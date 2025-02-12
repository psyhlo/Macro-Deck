﻿using SuchByte.MacroDeck.Events;
using SuchByte.MacroDeck.Folders;
using SuchByte.MacroDeck.GUI;
using SuchByte.MacroDeck.GUI.CustomControls;
using SuchByte.MacroDeck.InternalPlugins.Variables.Models;
using SuchByte.MacroDeck.Language;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Plugins;
using SuchByte.MacroDeck.Profiles;
using SuchByte.MacroDeck.Variables.Plugin.GUI;
using SuchByte.MacroDeck.Variables.Plugin.Models;
using SuchByte.MacroDeck.Variables.Plugin.Views;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Timers;

namespace SuchByte.MacroDeck.Variables.Plugin // Don't change because of backwards compatibility!
{
    public class VariablesPlugin : MacroDeckPlugin
    {
        internal override string Name => LanguageManager.Strings.PluginMacroDeckVariables;
        internal override string Author => "Macro Deck";

        internal override Image PluginIcon => Properties.Resources.Variable_Normal;

        private VariableChangedEvent variableChangedEvent = new VariableChangedEvent();

        Timer timeDateTimer;

        public override void Enable()
        {
            this.Actions = new List<PluginAction>
            {
                new ChangeVariableValueAction(),
                new SaveVariableToFileAction(),
                new ReadVariableFromFileAction(),
            };
            EventManager.RegisterEvent(this.variableChangedEvent);
            VariableManager.OnVariableChanged += VariableChanged;

            this.timeDateTimer = new Timer(1000)
            {
                Enabled = true
            };
            this.timeDateTimer.Elapsed += this.OnTimerTick;
            this.timeDateTimer.Start();
        }
        private void OnTimerTick(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                CultureInfo culture = new CultureInfo(LanguageManager.GetLanguageCode()); //Set CultureInfo locale by selected language

                VariableManager.SetValue("time", DateTime.Now.ToString("t"), VariableType.String, "Macro Deck");
                VariableManager.SetValue("date", DateTime.Now.ToString("d"), VariableType.String, "Macro Deck");
                VariableManager.SetValue("day_of_week", culture.DateTimeFormat.GetDayName(DateTime.Now.DayOfWeek), VariableType.String, "Macro Deck");
            });
        }
        private void VariableChanged(object sender, EventArgs e)
        {
            this.variableChangedEvent.Trigger(sender);
        }
    }

    public class VariableChangedEvent : IMacroDeckEvent
    {
        public string Name => "Variable changed";

        public EventHandler<MacroDeckEventArgs> OnEvent { get; set; }
        public List<string> ParameterSuggestions {
            get 
            {
                List<string> variables = new List<string>();
                foreach (Variable variable in VariableManager.ListVariables)
                {
                    variables.Add(variable.Name);
                }
                return variables;
            } set { }
        }

        public void Trigger(object sender)
        {
            if (this.OnEvent != null)
            {
                try
                {
                    foreach (MacroDeckProfile macroDeckProfile in ProfileManager.Profiles)
                    {
                        foreach (MacroDeckFolder folder in macroDeckProfile.Folders)
                        {
                            if (folder.ActionButtons == null) continue;
                            foreach (ActionButton.ActionButton actionButton in folder.ActionButtons.FindAll(actionButton => actionButton.EventListeners != null && actionButton.EventListeners.Find(x => x.EventToListen != null && x.EventToListen.Equals(this.Name)) != null))
                            {
                                MacroDeckEventArgs macroDeckEventArgs = new MacroDeckEventArgs
                                {
                                    ActionButton = actionButton,
                                    Parameter = ((Variable)sender).Name,
                                };
                                this.OnEvent(this, macroDeckEventArgs);
                            }
                        }
                    }
                }
                catch { }
            }
        }
    }

    public class ChangeVariableValueAction : PluginAction
    {
        public override string Name => LanguageManager.Strings.ActionChangeVariableValue;

        public override string Description => LanguageManager.Strings.ActionChangeVariableValue;

        public override bool CanConfigure => true;

        public override ActionConfigControl GetActionConfigControl(ActionConfigurator actionConfigurator)
        {
            return new ChangeVariableValueActionConfigView(this);
        }

        public override void Trigger(string clientId, ActionButton.ActionButton actionButton)
        {
            if (string.IsNullOrWhiteSpace(this.Configuration)) return;
            var changeVariableActionConfigModel = ChangeVariableValueActionConfigModel.Deserialize(this.Configuration);
            Variable variable = VariableManager.ListVariables.Where(v => v.Name == changeVariableActionConfigModel.Variable).FirstOrDefault();
            if (variable == null) return;
            switch (changeVariableActionConfigModel.Method)
            {

                case InternalPlugins.Variables.Enums.ChangeVariableMethod.countUp:
                    VariableManager.SetValue(variable.Name, float.Parse(variable.Value) + 1, (VariableType)Enum.Parse(typeof(VariableType), variable.Type), variable.Creator);
                    break;
                case InternalPlugins.Variables.Enums.ChangeVariableMethod.countDown:
                    VariableManager.SetValue(variable.Name, float.Parse(variable.Value) - 1, (VariableType)Enum.Parse(typeof(VariableType), variable.Type), variable.Creator);
                    break;
                case InternalPlugins.Variables.Enums.ChangeVariableMethod.set:
                    var value = VariableManager.RenderTemplate(changeVariableActionConfigModel.Value);
                    VariableManager.SetValue(variable.Name, value, (VariableType)Enum.Parse(typeof(VariableType), variable.Type), variable.Creator);
                    break;
                case InternalPlugins.Variables.Enums.ChangeVariableMethod.toggle:
                    VariableManager.SetValue(variable.Name, !bool.Parse(variable.Value.Replace("on", "true")), (VariableType)Enum.Parse(typeof(VariableType), variable.Type), variable.Creator);
                    break;

            }
        }
    }

    public class SaveVariableToFileAction : PluginAction
    {
        public override string Name => LanguageManager.Strings.ActionSaveVariableToFile;

        public override string Description => LanguageManager.Strings.ActionSaveVariableToFileDescription;

        public override bool CanConfigure => true;

        public override void Trigger(string clientId, ActionButton.ActionButton actionButton)
        {
            var configurationModel = ReadVariableFromFileActionConfigModel.Deserialize(this.Configuration);
            if (configurationModel == null) return;
            var filePath = configurationModel.FilePath;
            var variable = VariableManager.ListVariables.Where(x => x.Name == configurationModel.Variable).FirstOrDefault();
            string variableValue;
            if (variable == null)
            {
                variableValue = "Variable not found";
            } else
            {
                variableValue = variable.Value;
            }
            try
            {
                Utils.Retry.Do(new Action(() =>
                {
                    File.WriteAllText(filePath, variableValue);
                })); 
            } catch (Exception ex)
            {
                MacroDeckLogger.Error(typeof(VariablesPlugin), $"Failed to save variable value to file: {ex.Message}");
            }
        }

        public override ActionConfigControl GetActionConfigControl(ActionConfigurator actionConfigurator)
        {
            return new SaveVariableToFileActionConfigView(this);
        }

    }
    public class ReadVariableFromFileAction : PluginAction
    {
        public override string Name => LanguageManager.Strings.ActionReadVariableFromFile;

        public override string Description => LanguageManager.Strings.ActionReadVariableFromFileDescription;

        public override bool CanConfigure => true;

        public override void Trigger(string clientId, ActionButton.ActionButton actionButton)
        {
            var configurationModel = SaveVariableToFileActionConfigModel.Deserialize(this.Configuration);
            if (configurationModel == null) return;
            var filePath = configurationModel.FilePath;
            var variable = VariableManager.ListVariables.Where(x => x.Name == configurationModel.Variable).FirstOrDefault();
            try
            {
                Utils.Retry.Do(new Action(() =>
                {
                    var value = File.ReadAllText(filePath).Trim();
                    switch (variable.Type)
                    {
                        case nameof(VariableType.Bool):
                            if (bool.TryParse(value, out bool valueBool))
                            {
                                VariableManager.SetValue(variable.Name, valueBool, VariableType.Bool);
                            }
                            break;
                        case nameof(VariableType.Float):
                            if (float.TryParse(value, out float valueFloat))
                            {
                                VariableManager.SetValue(variable.Name, valueFloat, VariableType.Float);
                            }
                            break;
                        case nameof(VariableType.Integer):
                            if (Int32.TryParse(value, out int valueInt))
                            {
                                VariableManager.SetValue(variable.Name, valueInt, VariableType.Integer);
                            }
                            break;
                        case nameof(VariableType.String):
                            VariableManager.SetValue(variable.Name, value, VariableType.String);
                            break;
                    }
                }));
            }
            catch (Exception ex)
            {
                MacroDeckLogger.Error(typeof(VariablesPlugin), $"Failed to read variable value from file: {ex.Message}");
            }
        }

        public override ActionConfigControl GetActionConfigControl(ActionConfigurator actionConfigurator)
        {
            return new ReadVariableFromFileActionConfigView(this);
        }

    }
}

﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SuchByte.MacroDeck.Models
{
    public class NotificationModel
    {
        public string SenderName { get; set; }

        public string Title { get; set; }

        public string Message { get; set; }

        public string Id { get; set; } = Guid.NewGuid().ToString();

        public long Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

        public List<Control> AdditionalControls { get; set; }

        public Bitmap CustomImage { get; set; }


    }
}

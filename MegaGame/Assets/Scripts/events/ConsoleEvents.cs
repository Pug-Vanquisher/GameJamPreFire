using System;
using UnityEngine;

namespace Events
{
    public enum ConsoleSender { Robot, Enemy, World }

    public readonly struct ConsoleMessage
    {
        public readonly DateTime Ts;
        public readonly ConsoleSender Sender;
        public readonly string Text;

        public ConsoleMessage(ConsoleSender sender, string text)
        {
            Ts = DateTime.Now;
            Sender = sender;
            Text = text;
        }
    }
}

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

    public readonly struct ConsoleScrollRequest { public readonly float Delta; public ConsoleScrollRequest(float d) { Delta = d; } }

    public readonly struct ConsoleMoveInput { public readonly UnityEngine.Vector2 Dir; public ConsoleMoveInput(UnityEngine.Vector2 d) { Dir = d; } }

    public readonly struct CommandExecutionStarted
    {
        public readonly string Title;     
        public readonly float Duration;   
        public CommandExecutionStarted(string t, float d) { Title = t; Duration = d; }
    }
    public readonly struct CommandExecutionFinished
    {
        public readonly string Title;
        public readonly string ResultText;
        public CommandExecutionFinished(string t, string r) { Title = t; ResultText = r; }
    }
}

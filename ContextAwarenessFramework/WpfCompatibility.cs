using System;

namespace System.Windows.Threading
{
    public class Dispatcher
    {
        public static Dispatcher CurrentDispatcher { get; } = new Dispatcher();
        public void Invoke(Action action) => action();
    }
}

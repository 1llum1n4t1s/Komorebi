using System;

namespace Komorebi.Models
{
    public class Count : IDisposable
    {
        public int Value { get; set; } = 0;

        public Count(int value)
        {
            Value = value;
        }

        public void Dispose()
        {
            // Ignore
        }
    }
}

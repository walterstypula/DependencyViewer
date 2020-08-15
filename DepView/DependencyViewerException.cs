using System;

namespace DepView
{
    public sealed class DependencyViewerException : Exception
    {
        public DependencyViewerException()
        {
        }

        public DependencyViewerException(string message) : base(message)
        {
        }

        public DependencyViewerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
using System.Collections.Generic;

namespace Shutter.Core;

public interface IStealthConfig
{
    IReadOnlyList<string> SuppressOnSuccess { get; }
    IReadOnlyList<string> NeverSuppress { get; }
}

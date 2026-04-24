using System.Threading.Tasks;

namespace Shutter.Core;

public interface IEncoder
{
    Task<string> EncodeAsync(string wavPath, string outputPath, QualityPreset preset);
}

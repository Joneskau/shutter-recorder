using System.Threading.Tasks;
using NAudio.Lame;
using NAudio.Wave;
using Shutter.Core;

namespace Shutter.App;

public class Mp3Encoder : IEncoder
{
    public Task<string> EncodeAsync(string wavPath, string outputPath, QualityPreset preset)
    {
        return Task.Run(() =>
        {
            var bitRate = preset switch
            {
                QualityPreset.Low => 96,
                QualityPreset.High => 320,
                _ => 192
            };

            using var reader = new AudioFileReader(wavPath);
            using var writer = new LameMP3FileWriter(outputPath, reader.WaveFormat, bitRate);
            reader.CopyTo(writer);

            return outputPath;
        });
    }
}

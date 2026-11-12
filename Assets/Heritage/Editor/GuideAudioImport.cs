using UnityEditor;
using UnityEngine;

namespace VirtualHeritage.Editor
{
    // Keeps the narration clips small: mono, Vorbis, compressed in memory. Applies to everything under Resources/Guide.
    public sealed class GuideAudioImport : AssetPostprocessor
    {
        public override uint GetVersion() => 2;   // bump to force a re-import of the clips

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Heritage/Resources/Guide/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true; importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory; settings.preloadAudioData = false;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .35f;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate; settings.sampleRateOverride = 24000;
            importer.defaultSampleSettings = settings;
        }
    }
}

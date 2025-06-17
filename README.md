# Overture Audio Export

### 1. Create AudioExport and Add Clips

```csharp
var export = new AudioExport();

// Add clips with timing and envelopes
export.AddClip(drumClip, 0f, Envelope.Full());
export.AddClip(bassClip, 1.5f, Envelope.Stop(duration: 4f));
export.AddClip(leadClip, 2f, Envelope.GentleStop(duration: 6f, volume: 0.8f));
```

### 2. Export to File

```csharp
var result = await AudioExport.ToFileAsync(export);
if (result.Success)
    Debug.Log($"Exported to: {result.PathOrError}");
```

### 3. Upload to Platform

```csharp
var config = new AudioSave.Config("My Song", "gameId", 120, 
    new[] { "electronic", "upbeat" }, "Custom description");

var uploadResult = await AudioSave.HandleFileAsync(result.PathOrError, config);
if (uploadResult.Success)
    Debug.Log($"Uploaded! Song ID: {uploadResult.SongId}");
```

## Options

```csharp
var options = new AudioExport.Options(44100, 2, 16); // Sample rate, channels, bits
var export = new AudioExport(options);
```

Default: Infers sample rate from first clip, stereo, 16-bit.
# Breachpoint Audio

A Unity 6.4 audio package with reusable cue assets and pooled playback for one-shot and looping sounds.

## Install

In Unity, open Package Manager, select **+ → Install package from tarball**, and choose the package's `.tgz` file. BREACHPOINT uses the embedded copy under its `Packages` folder.

## Set up a scene

1. Add **Audio Service** to a GameObject. Use one service in the scene and keep an **Audio Listener** on the active camera.
2. Create a cue through **Assets → Create → Breachpoint → Audio → Audio Cue**. Assign one or more clips, set **Spatial Blend** to `0` for 2D or `1` for 3D, and assign a mixer group if the project uses a mixer.
3. Give the gameplay script a reference to the service and the cue. Scripts in custom assemblies can reference `Breachpoint.Audio` in their assembly definition.

Example component for a one-shot:

```csharp
using Breachpoint.Audio;
using UnityEngine;

public sealed class PlaySound : MonoBehaviour
{
    [SerializeField] private AudioService _audioService;
    [SerializeField] private AudioCue _cue;

    public void Play()
    {
        _audioService.Play(_cue, transform.position);
    }
}
```

Use `Play(cue, followTarget)` to follow a moving object, or `PlayLoop(cue, position)` and `PlayLoop(cue, followTarget)` for loops. Call `Stop()` on the returned `AudioHandle` to end a loop early. A loop attached to a destroyed target stops automatically. A one-shot finishes at the target's last position.

**Prewarm Source Count** controls how many sources the service creates at startup. The default is 48. The pool reuses available sources and grows by one when all sources are busy. Unity's voice limits under **Project Settings → Audio** govern how many sounds can be audible at once.

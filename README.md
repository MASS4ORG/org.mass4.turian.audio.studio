# Audio Mixer Studio

`org.mass4.turian.audio.studio` adds an editor panel to Turian Studio; it is not an in-game user interface. Install it in the host with `turian brick add org.mass4.turian.audio.studio file:<source> --studio`, then open **View → Audio Mixer** during Play. The project needs `org.mass4.turian.audio`; assign an Audio Mixer asset through Audio Settings to define buses, effect chains, snapshots and ducking.

The panel provides per-bus decibel faders, mute/solo, independent stereo peak/RMS meters and snapshot auditioning with a transition duration. Controls affect the active Play session; restarting Play restores authored state. Asset changes belong in the mixer's Inspector while Play is stopped. Saturated command queues report a retry message.

The panel uses Gaya's public plugin/panel SDK and the runtime `IAudioMixerService`. It acquires the current session on every frame, so stopping or replacing Play does not retain the previous session. Runtime projects do not load this editor brick, and applications can use another UI library or no GUI.

Build `Brick~/Tests/Turian.Audio.Studio.Tests` against a sibling Turian checkout, or supply `-p:TurianDir=/path/to/Turian/`. Tests render the actual panel offscreen and exercise mute, solo, snapshots and session replacement.

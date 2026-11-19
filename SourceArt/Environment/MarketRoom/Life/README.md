# Market daily use

User-authorized small props and a guarded rotating desk fan, authored separately from the accepted room/counter/devices.

Run build_daily_use.py with Blender in background mode. It exports DailyUse.fbx (static props and fan body) and FanRotor.fbx (local origin, X-axis spin) into the owned MarketRoom/Life directory. DailyUse.blend and FanRotor.blend are editable source files. Static objects are bounded to the two existing counter tops before export. No existing art file is opened or overwritten.

Contents: three styluses/pencils, pocket notebook with short strokes, two loose packing labels, a used hollow cup and one fan. The fan has a weighted foot, support, rolled shroud, domed concentric guard/spokes and three swept rotor blades. Existing HDRP finishes are reused; only matte LifePaper and LifeDrink are added. No lights, colliders or rigidbodies are generated.

Unity bridge commands: market-life-create, market-life-validate, market-life-capture, market-life-playtest. The creation command installs PF_MarketDailyUse under root15, remaps materials and serializes the AmbientFan rotor reference. Its speed starts at160 degrees/second (about27rpm) about local X, using scaled delta time. Disable the component to stop it; game pause also stops it.

The PlayMode probe uses an isolated, unlisted FanRuntimeProbe scene with only this art prefab. It checks rotation, fixed pivot, Time.timeScale=0, disabled-component behavior and resuming. It restores Editor start-scene and background/time settings, leaving the actual combat scene and actors untouched. Reports and HDRP captures are in Logs/BattlefieldMarketLife. No combat-route run or navigation bake is part of this pass.

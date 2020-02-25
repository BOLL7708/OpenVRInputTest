# OpenVRInputTest
A working example of listening to SteamVR input v2 using C#

This is a console application that will report incoming events.

It was used to very slowly get a working integration of the SteamVR input system introduced in 2018.
Finally in 2020, by talking to a range of people, this test implementation finally became functional.

Build for 64bit and it should just work, in theory.

## Key thing learned
Things that I don't think the OpenVR wiki told me very clearly.

* Need to include and load a `app.vrmanifest` with `AddApplicationManifest()`
* Make sure to provide the proper action-set handle in the `VRActiveActionSet_t` when running `UpdateActionState()`
* Last parameter in `GetDigitalActionData()` is not a device index, but a handle, can be set to 0 for no filter

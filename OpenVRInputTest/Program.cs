using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Valve.VR;

namespace OpenVRInputTest
{
    class Program
    {
        static ulong mActionSetHandle;
        static ulong mActionHandleLeftB, mActionHandleRightB, mActionHandleLeftA, mActionHandleRightA, mActionHandleChord1, mActionHandleChord2;
        static VRActiveActionSet_t[] mActionSetArray;
        static InputDigitalActionData_t[] mActionArray;

        // # items are referencing this list of actions: https://github.com/ValveSoftware/openvr/wiki/SteamVR-Input#getting-started
        static void Main(string[] args)
        {
            // Initializing connection to OpenVR
            var error = EVRInitError.None;
            OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background); // Had this as overlay before to get it working, but changing it back is now fine?
            var t = new Thread(Worker);
            if (error != EVRInitError.None) Utils.PrintError($"OpenVR initialization errored: {Enum.GetName(typeof(EVRInitError), error)}");
            else
            {
                Utils.PrintInfo("OpenVR initialized successfully.");

                // Load app manifest, I think this is needed for the application to show up in the input bindings at all
                Utils.PrintVerbose("Loading app.vrmanifest");
                var appError = OpenVR.Applications.AddApplicationManifest(Path.GetFullPath("./app.vrmanifest"), false);
                if (appError != EVRApplicationError.None) Utils.PrintError($"Failed to load Application Manifest: {Enum.GetName(typeof(EVRApplicationError), appError)}");
                else Utils.PrintInfo("Application manifest loaded successfully.");

                // #3 Load action manifest
                Utils.PrintVerbose("Loading actions.json");
                var ioErr = OpenVR.Input.SetActionManifestPath(Path.GetFullPath("./actions.json"));
                if (ioErr != EVRInputError.None) Utils.PrintError($"Failed to load Action Manifest: {Enum.GetName(typeof(EVRInputError), ioErr)}");
                else Utils.PrintInfo("Action Manifest loaded successfully.");

                // #4 Get action handles
                Utils.PrintVerbose("Getting action handles");

                var errorBL = OpenVR.Input.GetActionHandle("/actions/default/in/leftB", ref mActionHandleLeftB);
                if (errorBL != EVRInputError.None) Utils.PrintError($"GetActionHandle LeftB Error: {Enum.GetName(typeof(EVRInputError), errorBL)}");
                Utils.PrintDebug($"Action Handle leftB: {mActionHandleLeftB}");

                var errorBR = OpenVR.Input.GetActionHandle("/actions/default/in/rightB", ref mActionHandleRightB);
                if (errorBR != EVRInputError.None) Utils.PrintError($"GetActionHandle RightB Error: {Enum.GetName(typeof(EVRInputError), errorBR)}");
                Utils.PrintDebug($"Action Handle rightB: {mActionHandleRightB}");

                var errorAL = OpenVR.Input.GetActionHandle("/actions/default/in/leftA", ref mActionHandleLeftA);
                if (errorAL != EVRInputError.None) Utils.PrintError($"GetActionHandle LeftA Error: {Enum.GetName(typeof(EVRInputError), errorAL)}");
                Utils.PrintDebug($"Action Handle leftA: {mActionHandleLeftA}");

                var errorAR = OpenVR.Input.GetActionHandle("/actions/default/in/rightA", ref mActionHandleRightA);
                if (errorAR != EVRInputError.None) Utils.PrintError($"GetActionHandle RightA Error: {Enum.GetName(typeof(EVRInputError), errorAR)}");
                Utils.PrintDebug($"Action Handle rightA: {mActionHandleRightA}");

                var errorC1 = OpenVR.Input.GetActionHandle("/actions/default/in/chord1", ref mActionHandleChord1);
                if (errorC1 != EVRInputError.None) Utils.PrintError($"GetActionHandle Chord1 Error: {Enum.GetName(typeof(EVRInputError), errorC1)}");
                Utils.PrintDebug($"Action Handle rightA: {mActionHandleChord1}");

                var errorC2 = OpenVR.Input.GetActionHandle("/actions/default/in/chord2", ref mActionHandleChord2);
                if (errorC2 != EVRInputError.None) Utils.PrintError($"GetActionHandle Chord2 Error: {Enum.GetName(typeof(EVRInputError), errorC2)}");
                Utils.PrintDebug($"Action Handle rightA: {mActionHandleChord2}");

                // #5 Get action set handle
                Utils.PrintVerbose("Getting action set handle");
                var errorAS = OpenVR.Input.GetActionSetHandle("/actions/default", ref mActionSetHandle);
                if (errorAS != EVRInputError.None) Utils.PrintError($"GetActionSetHandle Error: {Enum.GetName(typeof(EVRInputError), errorAS)}");
                Utils.PrintDebug($"Action Set Handle default: {mActionSetHandle}");

                // Starting worker
                Utils.PrintDebug("Starting worker thread.");
                if (!t.IsAlive) t.Start();
                else Utils.PrintError("Could not start worker thread.");
            }
            Console.ReadLine();
            t.Abort();
            OpenVR.Shutdown();
        }

        private static void Worker()
        {
            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                // Getting events
                var vrEvents = new List<VREvent_t>();
                var vrEvent = new VREvent_t();
                try
                {
                    while (OpenVR.System.PollNextEvent(ref vrEvent, Utils.SizeOf(vrEvent)))
                    {
                        vrEvents.Add(vrEvent);
                    }
                }
                catch (Exception e)
                {
                    Utils.PrintWarning($"Could not get events: {e.Message}");
                }

                // Printing events
                foreach (var e in vrEvents)
                {
                    var pid = e.data.process.pid;
                    if ((EVREventType)vrEvent.eventType != EVREventType.VREvent_None)
                    {
                        var name = Enum.GetName(typeof(EVREventType), e.eventType);
                        var message = $"[{pid}] {name}";
                        if (pid == 0) Utils.PrintVerbose(message);
                        else if (name == null) Utils.PrintVerbose(message);
                        else if (name.ToLower().Contains("fail")) Utils.PrintWarning(message);
                        else if (name.ToLower().Contains("error")) Utils.PrintError(message);
                        else if (name.ToLower().Contains("success")) Utils.PrintInfo(message);
                        else Utils.Print(message);
                    }
                }

                // #6 Update action set
                if(mActionSetArray == null)
                {
                    var actionSet = new VRActiveActionSet_t
                    {
                        ulActionSet = mActionSetHandle,
                        ulRestrictedToDevice = OpenVR.k_ulInvalidActionSetHandle,
                        nPriority = 0
                    };
                    mActionSetArray = new VRActiveActionSet_t[] { actionSet };
                }

                var errorUAS = OpenVR.Input.UpdateActionState(mActionSetArray, (uint)Marshal.SizeOf(typeof(VRActiveActionSet_t)));
                if (errorUAS != EVRInputError.None) Utils.PrintError($"UpdateActionState Error: {Enum.GetName(typeof(EVRInputError), errorUAS)}");

                // #7 Load input action data
                if(mActionArray == null)
                {
                    mActionArray = new InputDigitalActionData_t[] {
                        new InputDigitalActionData_t(),
                        new InputDigitalActionData_t(),
                        new InputDigitalActionData_t(),
                        new InputDigitalActionData_t(),
                        new InputDigitalActionData_t(),
                        new InputDigitalActionData_t()
                    };
                }

                ulong leftHandle = 0;
                OpenVR.Input.GetInputSourceHandle("/user/hand/left", ref leftHandle);
                ulong rightHandle = 0;
                OpenVR.Input.GetInputSourceHandle("/user/hand/right", ref rightHandle);
                var handles = new ulong[] { leftHandle, rightHandle };
                foreach(var handle in handles)
                {
                    GetDigitalInput(mActionHandleLeftB, ref mActionArray[0], handle);
                    GetDigitalInput(mActionHandleRightB, ref mActionArray[1], handle);
                    GetDigitalInput(mActionHandleLeftA, ref mActionArray[2], handle);
                    GetDigitalInput(mActionHandleRightA, ref mActionArray[3], handle);
                    GetDigitalInput(mActionHandleChord1, ref mActionArray[4], handle);
                    GetDigitalInput(mActionHandleChord2, ref mActionArray[5], handle);
                }

                // Restrict rate
                Thread.Sleep(1000 / 10);
            }
        }

        static Dictionary<ulong, EVRInputError> inputErrors = new Dictionary<ulong, EVRInputError>();

        private static void GetDigitalInput(ulong handle, ref InputDigitalActionData_t action, ulong restrict)
        {
            var size = (uint)Marshal.SizeOf(typeof(InputDigitalActionData_t));
            var error = OpenVR.Input.GetDigitalActionData(handle, ref action, size, restrict);

            // Error
            if (inputErrors.ContainsKey(handle))
            {
                if (error != inputErrors[handle] && error != EVRInputError.None)
                {
                    Utils.PrintError($"DigitalActionDataError: {Enum.GetName(typeof(EVRInputError), error)}");
                }
                inputErrors[handle] = error;
            }

            // Result
            if (action.bChanged)
            {
                Utils.PrintInfo($"Action {handle}, Active: {action.bActive}, State: {action.bState} on: {restrict}");
            }
        }
    }
}


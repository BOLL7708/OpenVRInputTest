using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Valve.VR;

namespace OpenVRInputTest
{
    class Program
    {
        static ulong mActionSetHandle;
        static ulong mActionHandleLeftB;
        static ulong mActionHandleRightB;
        static EVRInputError mLastError;

        // # items are referencing this list of actions: https://github.com/ValveSoftware/openvr/wiki/SteamVR-Input#getting-started
        static void Main(string[] args)
        {
            // Initializing connection to OpenVR
            var error = EVRInitError.None;
            OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);
            var t = new Thread(Worker);
            if (error != EVRInitError.None) Utils.PrintError($"OpenVR initialization errored: {Enum.GetName(typeof(EVRInitError), error)}");
            else
            {
                Utils.PrintInfo("OpenVR initialized successfully.");

                // Load app manifest
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
                var errorAL = OpenVR.Input.GetActionHandle("/actions/default/in/leftB", ref mActionHandleLeftB);
                if (errorAL != EVRInputError.None) Utils.PrintError($"GetActionHandle LeftB Error: {Enum.GetName(typeof(EVRInputError), errorAL)}");
                Utils.PrintDebug($"Action Handle leftB: {mActionHandleLeftB}");

                var errorAR = OpenVR.Input.GetActionHandle("/actions/default/in/rightB", ref mActionHandleRightB);
                if (errorAR != EVRInputError.None) Utils.PrintError($"GetActionHandle RightB Error: {Enum.GetName(typeof(EVRInputError), errorAR)}");
                Utils.PrintDebug($"Action Handle rightB: {mActionHandleRightB}");

                // #5 Get action set handle
                Utils.PrintVerbose("Getting action set handle");
                var errorAS = OpenVR.Input.GetActionSetHandle("actions/default", ref mActionSetHandle);
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
                catch(Exception e)
                {
                    Utils.PrintWarning($"Could not get evemt: {e.Message}");
                }

                // Printing events
                foreach(var e in vrEvents)
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

                // #6 Update action set, pretty sure this is where things are broken right now.
                var actionSet = new VRActiveActionSet_t[1];
                actionSet[0] = new VRActiveActionSet_t();
                actionSet[0].ulActionSet = mActionSetHandle;
                // actionSet[0].nPriority = 1;

                var errorUAS = OpenVR.Input.UpdateActionState(actionSet, (uint) Marshal.SizeOf(typeof(VRActiveActionSet_t)));
                if (errorUAS != EVRInputError.None) Utils.PrintError($"UpdateActionState Error: {Enum.GetName(typeof(EVRInputError), errorUAS)}");
                // else Utils.PrintInfo($"Action set loaded: {actionSet[0].ulActionSet}");

                // Seems the action set is just zero? Means it doesn't work?

                /*
                // Seems I need the action set when updating the state of actions.
                var actionSet = new VRActiveActionSet_t
                {
                    ulActionSet = mActionSetHandle
                };
                var actionSetArr = new VRActiveActionSet_t[1] { actionSet };
                var activeActionSetSize = Utils.SizeOf(typeof(VRActiveActionSet_t));

                // But I cannot get the size of an array so I supply the one for the set inside the array.
                // Not really sure what I am supposed to do here (or above).
                var errorUAS = OpenVR.Input.UpdateActionState(actionSetArr, activeActionSetSize);
                if (errorUAS != EVRInputError.None)
                {
                    Utils.PrintError($"UpdateActionState Error: {Enum.GetName(typeof(EVRInputError), errorUAS)}");
                }
                */

                // Get input actions
                var roles = new ETrackedControllerRole[] { ETrackedControllerRole.LeftHand, ETrackedControllerRole.RightHand };
                // var role = ETrackedControllerRole.LeftHand;
                foreach (var role in roles)
                {
                    // Get device to restrict to, appears mandatory, makes sense for shared actions.
                    uint index = OpenVR.System.GetTrackedDeviceIndexForControllerRole(role);
                    // Utils.PrintVerbose($"Checking state for {Enum.GetName(typeof(ETrackedControllerRole), role)} ({index})");

                    // #7 Load input action data
                    var action = new InputDigitalActionData_t(); // I assume this is used for boolean inputs.
                    var size = Utils.SizeOf(action);
                    var error = OpenVR.Input.GetDigitalActionData(mActionHandleLeftB, ref action, size, index);
                
                    // Result
                    if (error != mLastError)
                    {
                        mLastError = error;
                        Utils.PrintError($"DigitalActionDataError: {Enum.GetName(typeof(EVRInputError), error)}");
                    }
                    if(action.bChanged)
                    {
                        Utils.PrintInfo($"Action state changed to: {action.bState}");
                    }
                    // Utils.PrintInfo($"Action state: {action.bState}");
                }

                // Restrict rate
                Thread.Sleep(1000 / 10);
            }
        }
    }
}

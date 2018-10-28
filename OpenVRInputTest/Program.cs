using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Valve.VR;

namespace OpenVRInputTest
{
    class Program
    {
        static ulong mActionSetHandle;
        static ulong mActionHandle;
        static EVRInputError mLastError;

        static void Main(string[] args)
        {
            // Initializing connection to OpenVR
            var error = EVRInitError.None;
            OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);
            if (error != EVRInitError.None) Utils.PrintError($"OpenVR initialization errored: {Enum.GetName(typeof(EVRInitError), error)}");
            else
            {
                Utils.PrintInfo("OpenVR initialized successfully.");

                // Loading manifests
                var appError = OpenVR.Applications.AddApplicationManifest(Path.GetFullPath("./app.vrmanifest"), false);
                if (appError != EVRApplicationError.None) Utils.PrintError($"Failed to load Application Manifest: {Enum.GetName(typeof(EVRApplicationError), appError)}");
                else Utils.PrintInfo("Application Manifest loaded successfully.");

                var ioErr = OpenVR.Input.SetActionManifestPath(Path.GetFullPath("./actions.json"));
                if (ioErr != EVRInputError.None) Utils.PrintError($"Failed to load Action Manifest: {Enum.GetName(typeof(EVRInputError), ioErr)}");
                else Utils.PrintInfo("Action Manifest loaded successfully.");

                // Getting action set handle
                var errorAS = OpenVR.Input.GetActionSetHandle("actions/default", ref mActionSetHandle);
                if (errorAS != EVRInputError.None) Utils.PrintError($"GetActionSetHandle Error: {Enum.GetName(typeof(EVRInputError), errorAS)}");
                Utils.PrintDebug($"Action Set Handle: {mActionSetHandle}");

                // Getting action handle.
                var errorA = OpenVR.Input.GetActionHandle("/actions/default/in/toggle_menu", ref mActionHandle);
                if (errorA != EVRInputError.None) Utils.PrintError($"GetActionHandle Error: {Enum.GetName(typeof(EVRInputError), errorA)}");
                Utils.PrintDebug($"Action Handle: {mActionHandle}");

                // Starting worker
                Utils.PrintDebug("Starting worker thread.");
                var t = new Thread(Worker);
                if (!t.IsAlive) t.Start();
                else Utils.PrintError("Could not start worker thread.");
            }
            Console.ReadLine();
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

                // Priting events
                foreach(var e in vrEvents)
                {
                    var pid = e.data.process.pid;
                    if ((EVREventType)vrEvent.eventType != EVREventType.VREvent_None)
                    {
                        var name = Enum.GetName(typeof(EVREventType), e.eventType);
                        var message = $"[{pid}] {name}";
                        if (pid == 0) Utils.PrintVerbose(message);
                        else if (name.ToLower().Contains("fail")) Utils.PrintWarning(message);
                        else if (name.ToLower().Contains("error")) Utils.PrintError(message);
                        else if (name.ToLower().Contains("success")) Utils.PrintInfo(message);
                        else Utils.Print(message);
                    }
                }

                // Update action set
                // Seems I need the action set when updating the state of actions.
                var actionSet = new VRActiveActionSet_t();
                actionSet.ulActionSet = mActionSetHandle;
                var actionSetArr = new VRActiveActionSet_t[1] { actionSet };

                // But I cannot get the size of an array so I supply the one for the set inside the array.
                // No really sure what I am actually supposed to do here (or above).
                var errorUAS = OpenVR.Input.UpdateActionState(actionSetArr, Utils.SizeOf(actionSet));
                if (errorUAS != EVRInputError.None)
                {
                    Utils.PrintError($"UpdateActionState Error: {Enum.GetName(typeof(EVRInputError), errorUAS)}");
                }

                // Get input actions
                var roles = new ETrackedControllerRole[] { ETrackedControllerRole.LeftHand, ETrackedControllerRole.RightHand };
                foreach (var role in roles)
                {
                    // Get device to restrict to, appears mandatory, makes sense for shared actions.
                    uint index = OpenVR.System.GetTrackedDeviceIndexForControllerRole(role);

                    // Load action data
                    var action = new InputDigitalActionData_t(); // I assume this is used for boolean inputs.
                    var size = Utils.SizeOf(action);
                    var error = OpenVR.Input.GetDigitalActionData(mActionHandle, ref action, size, index);

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
                }

                // Restrict rate
                Thread.Sleep(1000 / 30);
            }
        }
    }
}

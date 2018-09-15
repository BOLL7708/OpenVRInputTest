using System;
using System.IO;
using System.Threading;
using Valve.VR;

namespace OpenVRInputTest
{
    class Program
    {
        static ulong mActionHandle;

        static void Main(string[] args)
        {
            // Initializing connection to OpenVR
            var error = EVRInitError.None;
            var initialized = OpenVR.InitInternal(ref error, EVRApplicationType.VRApplication_Background);
            if (error != EVRInitError.None) Utils.PrintError($"OpenVR initialization errored: {Enum.GetName(typeof(EVRInitError), error)}");
            if (initialized <= 0) Utils.PrintError("OpenVR failed to initialize.");
            else
            {
                Utils.PrintInfo("OpenVR initialized successfully.");

                // Loading manifests
                var appError = OpenVR.Applications.AddApplicationManifest(Path.GetFullPath("./app.vrmanifest"), true);
                if (appError != EVRApplicationError.None) Utils.PrintError($"Failed to load Application Manifest: {Enum.GetName(typeof(EVRApplicationError), appError)}");
                else Utils.PrintInfo("Loaded Application Manifest successfully.");

                var ioErr = OpenVR.Input.SetActionManifestPath(Path.GetFullPath("./actions.json"));
                if (ioErr != EVRInputError.None) Utils.PrintError($"Failed to load Action Manifest: {Enum.GetName(typeof(EVRInputError), ioErr)}");
                else Utils.PrintInfo("Loaded Action Manifest successfully.");

                // Getting handle
                OpenVR.Input.GetActionHandle("/actions/main/in/Click", ref mActionHandle);

                // Starting worker
                Utils.PrintDebug("Starting worker thread.");
                var t = new Thread(Worker);
                if (!t.IsAlive) t.Start();
                else Utils.PrintError("Could not start worker thread.");
            }
            Console.ReadLine();
        }

        private static void Worker()
        {
            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                // Getting recent event, as we're running at 30 fps we'll just load them one per cycle.
                var vrEvent = new VREvent_t();
                uint eventSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(vrEvent);
                try { 
                    OpenVR.System.PollNextEvent(ref vrEvent, eventSize);
                    if((EVREventType) vrEvent.eventType != EVREventType.VREvent_None)
                    {
                        var name = Enum.GetName(typeof(EVREventType), vrEvent.eventType);
                        if(name.ToLower().Contains("fail")) Utils.PrintWarning($"{name}");
                        else if (name.ToLower().Contains("error")) Utils.PrintError($"{name}");
                        else Utils.Print($"{name}");
                    }
                }
                catch (Exception e)
                {
                    Utils.PrintWarning($"Could not get evemt: {e.Message}");
                }

                // Get input actions
                var roles = new ETrackedControllerRole[] { ETrackedControllerRole.LeftHand, ETrackedControllerRole.RightHand };
                foreach (var role in roles)
                {
                    var action = new InputDigitalActionData_t();
                    var size = System.Runtime.InteropServices.Marshal.SizeOf(action);
                    uint index = OpenVR.System.GetTrackedDeviceIndexForControllerRole(role);
                    OpenVR.Input.GetDigitalActionData(mActionHandle, ref action, (uint)size, index);
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

using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // Remote control should be in the middle of width direction.
        // Always place your rotors symmetrically.

        string TAG = "[Hover]";
        float target_altitude = 20;
        string Channel = "Drone channel";
        bool test_flight = true;

        List<IMyThrust> thrusters = new List<IMyThrust>();
        List<IMyMotorAdvancedStator> rotors = new List<IMyMotorAdvancedStator>();
        List<IMyGyro> gyros = new List<IMyGyro>();
        List<IMyRemoteControl> remotes = new List<IMyRemoteControl>();
        List<IMyCameraBlock> cameras = new List<IMyCameraBlock>();
        List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        List<IMyGasTank> tanks = new List<IMyGasTank>();
        List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
        List<IMyCargoContainer> containers = new List<IMyCargoContainer>();
        List<IMyShipDrill> drills = new List<IMyShipDrill>();
        IMyRemoteControl remote;
        IMyCubeGrid grid;
        IMyCameraBlock frontcam;
        int Cycle;
        int setup_passed;
        MyDetectedEntityInfo info;
        IMyBroadcastListener listener;

        //Measure Units
        double altitude;
        double speed;
        double gravity;
        double mass;
        Vector3D planetCenter;
        Vector3D left_point;
        Vector3D right_point;
        Vector3D front_point;
        Vector3D back_point;
        Vector3D top_point;
        Vector3D bottom_point;
        double front_planetcenter;
        double back_planetcenter;
        double left_planetcenter;
        double right_planetcenter;
        Vector3D Velocitydirection;
        double VelocityForward; 
        double VelocityLeft; 
        double VelocityRight; 
        double VelocityBackward;
        float thrustmultiplier;
        double last_altitude;
        double temptarget_altitude;
        double max_liftweight;
        double max_speed;
        bool headupdown;
        int leftrightthrustcount;

        //Math
        double FrontBackAngle;
        double LeftRightAngle;
        float leftrightvelocity;

        //Gyro directions
        float pitch;
        float yaw;
        float roll;

        //camera
        double distance = 100;
        float cpitch = 0;
        float cyaw = 0;

        //locations
        Vector3D mining_site;
        Vector3D home;
        Vector3D heading;
        Vector3D target;
        Vector3D temptarget;
        Vector3D last_pos;

        //status
        bool fuel_low;
        bool battery_low;
        bool cargo_full;
        bool idle;
        int idletimer;

        //roadblocks
        Vector3D hitpoint_forward;
        double dist_hpforward;
        Vector3D hitpoint_backward;
        double dist_hpbackward;
        Vector3D hitpoint_left;
        double dist_hpleft;
        Vector3D hitpoint_right;
        double dist_hpright;
        Vector3D hitpoint_up;
        double dist_hpup;
        Vector3D hitpoint_down;
        double dist_hpdown;
        float x, y, z;

        //Grid info
        double fuel;
        double batterycharge;
        double cargofilllevel;
        float grid_lenght;
        float grid_width;
        float grid_height;
        Vector3L left, right;
        Vector3L front, back;

        //Test Flight
        int test_steps;
        bool test_trigger;
        int test_timer;
        Vector3D test_start;
        Vector3D test_point;
        Vector3D temp_test_Target;

        //Mining
        bool mining_start_trigger;
        int containers_full;

        public Program()
        {
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters, x => x.CustomName.Contains(TAG));
            GridTerminalSystem.GetBlocksOfType<IMyMotorAdvancedStator>(rotors, x => x.CustomName.Contains(TAG));
            GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros, x => x.CustomName.Contains(TAG));
            GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(remotes, x => x.CustomName.Contains(TAG));
            GridTerminalSystem.GetBlocksOfType<IMyCameraBlock>(cameras, x => x.CustomName.Contains(TAG));
            GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(connectors, x => x.CustomName.Contains(TAG));
            GridTerminalSystem.GetBlocksOfType<IMyGasTank>(tanks, x => x.CustomName.Contains(TAG));
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries, x => x.CustomName.Contains(TAG));
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containers, x => x.CustomName.Contains(TAG));
            GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(drills, x => x.CustomName.Contains(TAG));
            grid = Me.CubeGrid;
            listener = IGC.RegisterBroadcastListener(Channel);
            listener.SetMessageCallback(Channel);
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }
        public void Main(string argument, UpdateType updateSource)
        {
            Echo(home.ToString());
            switch (Cycle)
            {
                case 0:
                    Setup();
                    break;
                case 1:
                    Measure();
                    if(idle == false) { Cycle++; Runtime.UpdateFrequency = UpdateFrequency.Update1; }
                    else { Runtime.UpdateFrequency = UpdateFrequency.Update100; }
                    break;
                case 2:
                    Cycle++;
                    FlightControl();
                    last_pos = remote.CenterOfMass;
                    break;
                case 3:
                    ControlGyro();
                    Cycle++;
                    break;
                case 4:
                    navigate();
                    Cycle++;
                    break;
                case 5:
                    SelfControl();
                    Cycle = 1;
                    break;
            }
            if ((updateSource & UpdateType.IGC) > 0)
            { 
                while (listener.HasPendingMessage)
                {
                    var IGCmessage = listener.AcceptMessage();
                    if (IGCmessage.Tag == Channel)
                    { 
                        string message = IGCmessage.Data.ToString();
                        if(message != "Home")
                        {
                            string[] msg = message.Split(':');
                            float.TryParse(msg[0], out x);
                            float.TryParse(msg[1], out y);
                            float.TryParse(msg[2], out z);
                            home = new Vector3D(x, y, z);
                        }
                    }
                    else
                    {
                        
                    }
                }
            }
        }
        public void HandleMessages()
        {
            if(home.X==0 && home.Y == 0 && home.Z == 0)
            {
                IGC.SendBroadcastMessage(Channel, "Home");
            }
        }
        public void Setup()
        {
            if (remotes.Count == 0)
            {
                Echo("No remotes found");
                Echo("Add remotes or tag them.");
                Runtime.UpdateFrequency = UpdateFrequency.Once;
            }
            else
            {
                Echo("Remote control found");
                setup_passed++;
                foreach (var rem in remotes)
                {
                    remote = rem;
                    break;
                }
            }
            if (gyros.Count == 0)
            {
                Echo("Gyro not found");
                Echo("Add gyro or tag them");
                Runtime.UpdateFrequency = UpdateFrequency.Once;
            }
            else
            {
                Echo("Gyro found");
                setup_passed++;
            }
            if (rotors.Count == 0)
            {
                Echo("Rotors not found");
                Echo("Add rotors or tag them");
                Runtime.UpdateFrequency = UpdateFrequency.Once;
            }
            else
            {
                Echo($"{rotors.Count} Rotors found");
                setup_passed++;
            }
            if (thrusters.Count == 0)
            {
                Echo("Thrusters not found");
                Echo("Add thrusters or tag them");
                Runtime.UpdateFrequency = UpdateFrequency.Once;
            }
            else
            {
                Echo($"{thrusters.Count} Thrusters found");
                setup_passed++;
            }
            if (setup_passed == 4)
            {
                Measure();
                SetupThrustersAndRotorsAndGyros();
            }
        }
        public void SetupThrustersAndRotorsAndGyros()
        {
            foreach (var rotor in rotors)
            {
                rotor.UpperLimitDeg = 180;
                rotor.LowerLimitDeg = -180;
                if (
                    Vector3D.Distance(rotor.GetPosition(), left_point) < Vector3D.Distance(rotor.GetPosition(), right_point) &&
                    Vector3D.Distance(rotor.GetPosition(), left_point) < Vector3D.Distance(rotor.GetPosition(), back_point) &&
                    Vector3D.Distance(rotor.GetPosition(), left_point) < Vector3D.Distance(rotor.GetPosition(), front_point)
                    )
                {
                    rotor.CustomName = "Rotor Left " + TAG;
                }
                else if (
                    Vector3D.Distance(rotor.GetPosition(), right_point) < Vector3D.Distance(rotor.GetPosition(), left_point) &&
                    Vector3D.Distance(rotor.GetPosition(), right_point) < Vector3D.Distance(rotor.GetPosition(), back_point) &&
                    Vector3D.Distance(rotor.GetPosition(), right_point) < Vector3D.Distance(rotor.GetPosition(), front_point)
                    )
                {
                    rotor.CustomName = "Rotor Right " + TAG;
                }
                else if (
                    Vector3D.Distance(rotor.GetPosition(), back_point) < Vector3D.Distance(rotor.GetPosition(), right_point) &&
                    Vector3D.Distance(rotor.GetPosition(), back_point) < Vector3D.Distance(rotor.GetPosition(), left_point) &&
                    Vector3D.Distance(rotor.GetPosition(), back_point) < Vector3D.Distance(rotor.GetPosition(), front_point)
                    )
                {
                    rotor.CustomName = "Rotor Back " + TAG;
                }
                else if (
                    Vector3D.Distance(rotor.GetPosition(), front_point) < Vector3D.Distance(rotor.GetPosition(), right_point) &&
                    Vector3D.Distance(rotor.GetPosition(), front_point) < Vector3D.Distance(rotor.GetPosition(), back_point) &&
                    Vector3D.Distance(rotor.GetPosition(), front_point) < Vector3D.Distance(rotor.GetPosition(), left_point)
                    )
                {
                    rotor.CustomName = "Rotor Front " + TAG;
                }
            }
            foreach (var rotor in thrusters)
            {
                if (
                    Vector3D.Distance(rotor.GetPosition(), left_point) < Vector3D.Distance(rotor.GetPosition(), right_point) &&
                    Vector3D.Distance(rotor.GetPosition(), left_point) < Vector3D.Distance(rotor.GetPosition(), back_point) &&
                    Vector3D.Distance(rotor.GetPosition(), left_point) < Vector3D.Distance(rotor.GetPosition(), front_point)
                    )
                {
                    rotor.CustomName = "Thruster Left " + TAG;
                    leftrightthrustcount++;
                }
                else if (
                    Vector3D.Distance(rotor.GetPosition(), right_point) < Vector3D.Distance(rotor.GetPosition(), left_point) &&
                    Vector3D.Distance(rotor.GetPosition(), right_point) < Vector3D.Distance(rotor.GetPosition(), back_point) &&
                    Vector3D.Distance(rotor.GetPosition(), right_point) < Vector3D.Distance(rotor.GetPosition(), front_point)
                    )
                {
                    rotor.CustomName = "Thruster Right " + TAG;
                    leftrightthrustcount++;
                }
                else if (
                    Vector3D.Distance(rotor.GetPosition(), back_point) < Vector3D.Distance(rotor.GetPosition(), right_point) &&
                    Vector3D.Distance(rotor.GetPosition(), back_point) < Vector3D.Distance(rotor.GetPosition(), left_point) &&
                    Vector3D.Distance(rotor.GetPosition(), back_point) < Vector3D.Distance(rotor.GetPosition(), front_point)
                    )
                {
                    rotor.CustomName = "Thruster Back " + TAG;
                }
                else if (
                    Vector3D.Distance(rotor.GetPosition(), front_point) < Vector3D.Distance(rotor.GetPosition(), right_point) &&
                    Vector3D.Distance(rotor.GetPosition(), front_point) < Vector3D.Distance(rotor.GetPosition(), back_point) &&
                    Vector3D.Distance(rotor.GetPosition(), front_point) < Vector3D.Distance(rotor.GetPosition(), left_point)
                    )
                {
                    rotor.CustomName = "Thruster Front " + TAG;
                }
            }
            foreach(var gyro in gyros)
            {
                gyro.Pitch = 0;
                gyro.Yaw = 0;
                gyro.Roll = 0;
            }
            Cycle++;
        }
        public void GridMeasures()
        {
            foreach(var rotor in rotors)
            {
                if (rotor.CustomName.Contains("Left"))
                {
                    left = rotor.Position;
                }
                else if (rotor.CustomName.Contains("Right"))
                {
                    right = rotor.Position;
                }
                else if (rotor.CustomName.Contains("Back"))
                {
                    back = rotor.Position;
                }
                else if (rotor.CustomName.Contains("Front"))
                {
                    front = rotor.Position;
                }
            }
            if (left.X != right.X && left.Y !=right.Y)
            {
                grid_width = grid.Max.Z - grid.Min.Z * grid.GridSize;
            }
            else if (left.Y != right.Y && left.Z != right.Z)
            {
                grid_width = grid.Max.X - grid.Min.X * grid.GridSize;
            }
            else if (left.Z != right.Z && left.X != right.X)
            {
                grid_width = grid.Max.Y - grid.Min.Y * grid.GridSize;
            }
            if (front.X == 0 && front.Y == 0 && front.Z == 0)
            {
                front = remote.Position;
            }

        }
        public void Measure()
        {
            last_altitude = altitude;
            temptarget_altitude = target_altitude;
            remote.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude);
            remote.TryGetPlanetPosition(out planetCenter);
            gravity = remote.GetNaturalGravity().Length();
            mass = remote.CalculateShipMass().TotalMass;
            speed = remote.GetShipSpeed();
            left_point = remote.CenterOfMass + remote.WorldMatrix.Left * 5;
            right_point = remote.CenterOfMass + remote.WorldMatrix.Right * 5;
            top_point = remote.CenterOfMass + remote.WorldMatrix.Up * 5;
            bottom_point = remote.CenterOfMass + remote.WorldMatrix.Down * 5;
            front_point = remote.CenterOfMass + remote.WorldMatrix.Forward * 5;
            back_point = remote.CenterOfMass + remote.WorldMatrix.Backward * 5;
            front_planetcenter = Vector3D.Distance(front_point, planetCenter);
            back_planetcenter = Vector3D.Distance(back_point, planetCenter);
            left_planetcenter = Vector3D.Distance(left_point, planetCenter);
            right_planetcenter = Vector3D.Distance(right_point, planetCenter);
            FrontBackAngle = (front_planetcenter - back_planetcenter) / (Vector3D.Distance(front_point, back_point) / 90) * Math.PI / 180;
            LeftRightAngle = (left_planetcenter - right_planetcenter) / (Vector3D.Distance(left_point, right_point) / 90) * Math.PI / 180;
            //Velocities
            Velocitydirection = new Vector3D(remote.CenterOfMass + remote.GetShipVelocities().LinearVelocity);
            VelocityForward = Math.Round(Vector3D.Distance(front_point, Velocitydirection));
            VelocityBackward = Math.Round(Vector3D.Distance(back_point, Velocitydirection));
            VelocityLeft = Math.Round(Vector3D.Distance(left_point, Velocitydirection));
            VelocityRight = Math.Round(Vector3D.Distance(right_point, Velocitydirection));
            leftrightvelocity = Convert.ToSingle(VelocityLeft - VelocityRight) / 5;
            if (leftrightvelocity < 0) { leftrightvelocity = leftrightvelocity * -1; }
            if (leftrightvelocity > 0.1) { leftrightvelocity = 0.1f; }
            if(target.X == 0 && target.Y == 0 && target.Z == 0 && idletimer<300) { idletimer++; if (home.X == 0) { HandleMessages(); } }
            else if(target.X == 0 && target.Y == 0 && target.Z == 0 && idletimer >= 299) { temptarget_altitude = 0; }
            else if(target.X != 0 && target.Y != 0 && target.Z != 0) { idletimer = 0; idle = false; temptarget_altitude = target_altitude; }
            if (temptarget_altitude == 0 && altitude < 2) { idle = true; }
            if (Vector3D.Distance(remote.GetPosition(), temptarget) < 10) { temptarget = new Vector3D(0,0,0); }
            max_liftweight = 0;
            foreach(var thruster in thrusters)
            {
                max_liftweight = max_liftweight + thruster.MaxThrust;
            }
            max_liftweight = max_liftweight / gravity * 3;
            max_speed = Vector3D.Distance(remote.GetPosition(), target) /8;
            if (max_speed > 20) { max_speed = 20; }
            if(front_planetcenter + 2.3 < back_planetcenter - 2.3) { headupdown = true; } else { headupdown = false; }
        }
        public void SelfControl()
        {
            fuel = 0;
            batterycharge = 0;
            cargofilllevel = 0;
            containers_full = 0;
            foreach(var tank in tanks)
            {
                CalculateFuel(tank);
            }
            foreach(var battery in batteries)
            {
                CalculateCharge(battery);
            }
            foreach(var container in containers)
            {
                CalculateCargo(container);
            }
            fuel = (fuel / tanks.Count) * 100;
            batterycharge = batterycharge / batteries.Count;
            cargofilllevel = cargofilllevel / containers.Count;
            if (fuel < 40) { fuel_low = true; }else if (fuel_low) { if (fuel >= 99) { fuel_low = false; } }
            if (batterycharge < 40) { battery_low = true; } else if (battery_low) { if (batterycharge >= 95) { battery_low = false; } }
            if(mass - 500 >= max_liftweight) { cargo_full = true; }
            if (containers_full == containers.Count) { cargo_full = true; }
        }
        public void CalculateFuel(IMyGasTank tank)
        {
            fuel = fuel + tank.FilledRatio;
        }
        public void CalculateCharge(IMyBatteryBlock battery)
        {
            batterycharge = batterycharge + (100 / battery.MaxStoredPower * battery.CurrentStoredPower); 
        }
        public void CalculateCargo(IMyCargoContainer container)
        {
            cargofilllevel = cargofilllevel + (100 / container.GetInventory().MaxVolume.RawValue * container.GetInventory().CurrentVolume.RawValue);
            if (container.GetInventory().IsFull) { containers_full++; }
        }
        public void OrientationCalculator(IMyGyro gyro)
        {
            Base6Directions.Direction gyro_up = remote.Orientation.TransformDirectionInverse(gyro.Orientation.Up);
            Base6Directions.Direction gyro_forward = remote.Orientation.TransformDirectionInverse(gyro.Orientation.Forward);
            switch (gyro_up)
            {
                case Base6Directions.Direction.Forward:
                    gyro.Yaw = roll * -1;
                    switch (gyro_forward)
                    {
                        case Base6Directions.Direction.Left:
                            gyro.Pitch = yaw;
                            gyro.Roll = pitch;
                            break;
                        case Base6Directions.Direction.Right:
                            gyro.Pitch = yaw * -1;
                            gyro.Roll = pitch * -1;
                            break;
                        case Base6Directions.Direction.Up:
                            gyro.Pitch = pitch * -1;
                            gyro.Roll = yaw * -1;
                            break;
                        case Base6Directions.Direction.Down:
                            gyro.Pitch = pitch;
                            gyro.Roll = yaw;
                            break;
                    }
                    break;
                case Base6Directions.Direction.Backward:
                    gyro.Yaw = roll;
                    switch (gyro_forward)
                    {
                        case Base6Directions.Direction.Left:
                            gyro.Pitch = yaw * -1;
                            gyro.Roll = pitch * -1;
                            break;
                        case Base6Directions.Direction.Right:
                            gyro.Pitch = yaw;
                            gyro.Roll = pitch;
                            break;
                        case Base6Directions.Direction.Up:
                            gyro.Pitch = pitch;
                            gyro.Roll = yaw;
                            break;
                        case Base6Directions.Direction.Down:
                            gyro.Pitch = pitch * -1;
                            gyro.Roll = yaw * -1;
                            break;
                    }
                    break;
                case Base6Directions.Direction.Left:
                    gyro.Yaw = pitch;
                    switch (gyro_forward)
                    {
                        case Base6Directions.Direction.Forward:
                            gyro.Pitch = yaw * -1;
                            gyro.Roll = roll;
                            break;
                        case Base6Directions.Direction.Backward:
                            gyro.Pitch = yaw;
                            gyro.Roll = roll * -1;
                            break;
                        case Base6Directions.Direction.Up:
                            gyro.Pitch = roll * -1;
                            gyro.Roll = yaw * -1;
                            break;
                        case Base6Directions.Direction.Down:
                            gyro.Pitch = roll;
                            gyro.Roll = yaw;
                            break;
                    }
                    break;
                case Base6Directions.Direction.Right:
                    gyro.Yaw = pitch * -1;
                    switch (gyro_forward)
                    {
                        case Base6Directions.Direction.Forward:
                            gyro.Pitch = yaw;
                            gyro.Roll = roll;
                            break;
                        case Base6Directions.Direction.Backward:
                            gyro.Pitch = yaw * -1;
                            gyro.Roll = roll * -1;
                            break;
                        case Base6Directions.Direction.Up:
                            gyro.Pitch = roll;
                            gyro.Roll = yaw * -1;
                            break;
                        case Base6Directions.Direction.Down:
                            gyro.Pitch = roll * -1;
                            gyro.Roll = yaw;
                            break;
                    }
                    break;
                case Base6Directions.Direction.Up:
                    gyro.Yaw = yaw;
                    switch (gyro_forward)
                    {
                        case Base6Directions.Direction.Forward:
                            gyro.Pitch = pitch;
                            gyro.Roll = roll;
                            break;
                        case Base6Directions.Direction.Backward:
                            gyro.Pitch = pitch * -1;
                            gyro.Roll = roll * -1;
                            break;
                        case Base6Directions.Direction.Left:
                            gyro.Pitch = roll;
                            gyro.Roll = pitch * -1;
                            break;
                        case Base6Directions.Direction.Right:
                            gyro.Pitch = roll * -1;
                            gyro.Roll = pitch;
                            break;
                    }
                    break;
                case Base6Directions.Direction.Down:
                    gyro.Yaw = yaw * -1;
                    switch (gyro_forward)
                    {
                        case Base6Directions.Direction.Forward:
                            gyro.Pitch = pitch * -1;
                            gyro.Roll = roll;
                            break;
                        case Base6Directions.Direction.Backward:
                            gyro.Pitch = pitch;
                            gyro.Roll = roll * -1;
                            break;
                        case Base6Directions.Direction.Left:
                            gyro.Pitch = roll * -1;
                            gyro.Roll = pitch * -1;
                            break;
                        case Base6Directions.Direction.Right:
                            gyro.Pitch = roll;
                            gyro.Roll = pitch;
                            break;
                    }
                    break;
            }
        }
        public void navigate()
        {
            foreach (var camera in cameras)
            {
                Scanning(camera);
                Base6Directions.Direction camera_forward = remote.Orientation.TransformDirectionInverse(camera.Orientation.Forward);
                switch (camera_forward)
                {
                    case Base6Directions.Direction.Forward:
                        if(info.HitPosition != null)
                        {
                            hitpoint_forward = new Vector3D(info.HitPosition.GetValueOrDefault().X, info.HitPosition.GetValueOrDefault().Y, info.HitPosition.GetValueOrDefault().Z);
                            dist_hpforward = Vector3D.Distance(hitpoint_forward, camera.GetPosition());
                            frontcam = camera;
                        }
                        else
                        {
                            hitpoint_forward = new Vector3D();
                            dist_hpforward = 101;
                        }
                        break;
                    case Base6Directions.Direction.Backward:
                        if (info.HitPosition != null)
                        {
                            hitpoint_backward = new Vector3D(info.HitPosition.GetValueOrDefault().X, info.HitPosition.GetValueOrDefault().Y, info.HitPosition.GetValueOrDefault().Z);
                            dist_hpbackward = Vector3D.Distance(hitpoint_backward, camera.GetPosition());
                        }
                        else
                        {
                            hitpoint_backward = new Vector3D();
                            dist_hpbackward = 101;
                        }
                        break;
                    case Base6Directions.Direction.Left:
                        if (info.HitPosition != null)
                        {
                            hitpoint_left = new Vector3D(info.HitPosition.GetValueOrDefault().X, info.HitPosition.GetValueOrDefault().Y, info.HitPosition.GetValueOrDefault().Z);
                            dist_hpleft = Vector3D.Distance(hitpoint_left, camera.GetPosition());
                        }
                        else
                        {
                            hitpoint_left = new Vector3D();
                            dist_hpleft = 101;
                        }
                        break;
                    case Base6Directions.Direction.Right:
                        if (info.HitPosition != null)
                        {
                            hitpoint_right = new Vector3D(info.HitPosition.GetValueOrDefault().X, info.HitPosition.GetValueOrDefault().Y, info.HitPosition.GetValueOrDefault().Z);
                            dist_hpright = Vector3D.Distance(hitpoint_right, camera.GetPosition());
                        }
                        else
                        {
                            hitpoint_right = new Vector3D();
                            dist_hpright = 101;
                        }
                        break;
                    case Base6Directions.Direction.Up:
                        if (info.HitPosition != null)
                        {
                            hitpoint_up = new Vector3D(info.HitPosition.GetValueOrDefault().X, info.HitPosition.GetValueOrDefault().Y, info.HitPosition.GetValueOrDefault().Z);
                            dist_hpup = Vector3D.Distance(hitpoint_up, camera.GetPosition());
                        }
                        else
                        {
                            hitpoint_up = new Vector3D();
                            dist_hpup = 101;
                        }
                        break;
                    case Base6Directions.Direction.Down:
                        if (info.HitPosition != null)
                        {
                            hitpoint_down = new Vector3D(info.HitPosition.GetValueOrDefault().X, info.HitPosition.GetValueOrDefault().Y, info.HitPosition.GetValueOrDefault().Z);
                            dist_hpdown = Vector3D.Distance(hitpoint_down, camera.GetPosition());
                        }
                        else
                        {
                            hitpoint_down = new Vector3D();
                            dist_hpdown = 101;
                        }
                        break;
                }
            }
        }
        public void FlightControl()
        {
            if (test_flight)
            {
                TestFlight();
            }
            else
            {
                if (cargo_full || fuel_low || battery_low || mining_site.X == 0)
                {
                    target = home;
                }
                else
                {
                    target = mining_site;
                }
            }
            if (mining_start_trigger)
            {
                if(mass<max_liftweight - 500)
                {
                    foreach (var drill in drills)
                    {
                        drill.Enabled = true;
                    }
                }
                else
                {
                    foreach (var drill in drills)
                    {
                        drill.Enabled = false;
                    }
                }
            }
        }
        public void PathFinder()
        {
            HoverGyro();
            if (Vector3D.Distance(heading, remote.GetPosition()) < 10) { heading.X = 0;heading.Y = 0;heading.Z = 0; }
            if (Vector3D.Distance(remote.GetPosition(), target) > 15)
            {
                if (dist_hpforward < 40)
                {
                    if(altitude<100 && dist_hpup > 102 - altitude)
                    {
                        target_altitude = target_altitude + 0.1f;
                    }
                    else if (dist_hpleft < dist_hpright)
                    {
                        temptarget = remote.GetPosition() + remote.WorldMatrix.Right * 30;
                    }
                    else if (dist_hpright < dist_hpleft)
                    {
                        temptarget = remote.GetPosition() + remote.WorldMatrix.Left * 30;
                    }
                }
                if (
                    Vector3D.Distance(front_point, target) - 2 < Vector3D.Distance(back_point, target)
                    && Vector3D.Distance(back_point, target) - 2 < Vector3D.Distance(front_point, target)
                    && Vector3D.Distance(left_point, target) - 2 < Vector3D.Distance(right_point, target)
                    && Vector3D.Distance(right_point, target) - 2 < Vector3D.Distance(left_point, target)
                    )
                {
                    foreach(var connector in connectors)
                    {
                        if (Vector3D.Distance(connector.GetPosition(), target) > Vector3D.Distance(bottom_point, target))
                        {
                            target_altitude = target_altitude - 0.01f;
                        }
                    }
                }
                TurnLeftRightGyro();
                foreach(var rotor in rotors)
                {
                    HoverBackFrontRotors(rotor);
                    if (Vector3D.Distance(front_point, target) > Vector3D.Distance(back_point, target))
                    {
                        HoverLeftRightRotors(rotor);
                    }
                    else
                    {
                        if (dist_hpforward < 20)
                        {
                            HoverBackFrontRotors(rotor);
                            HoverLeftRightRotors(rotor);
                        }
                        else
                        {
                            if (VelocityForward > VelocityBackward)
                            {
                                FlyForwardRotors(rotor);
                            }
                            else
                            {
                                if (speed < max_speed)
                                {
                                    FlyForwardRotors(rotor);
                                }
                                else if (speed > max_speed)
                                {
                                    FlyBackwardRotors(rotor);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var rotor in rotors)
                {
                    if (Vector3D.Distance(left_point, target) < Vector3D.Distance(right_point, target))
                    {
                        FlyLeftRotors(rotor);
                    }
                    else if (Vector3D.Distance(left_point, target) > Vector3D.Distance(right_point, target))
                    {
                        FlyRightRotors(rotor);
                    }
                    if(Vector3D.Distance(front_point, target) < Vector3D.Distance(back_point, target))
                    {
                        FlyForwardRotors(rotor);
                    }
                    else if (Vector3D.Distance(front_point, target) > Vector3D.Distance(back_point, target))
                    {
                        FlyBackwardRotors(rotor);
                    }
                }
                
            }
            foreach (var thruster in thrusters)
            {
                HoverThrust(thruster);
            }
            if (altitude > 5 && Vector3D.Distance(bottom_point, target) < Vector3D.Distance(top_point, target))
            {
                target_altitude = target_altitude - 0.01f;
            }
            else if (altitude > 5 && Vector3D.Distance(bottom_point, target) > Vector3D.Distance(top_point, target))
            {
                target_altitude = target_altitude + 0.01f;
            }
        }
        public void ControlGyro()
        {
            foreach (var gyro in gyros)
            {
                gyro.Enabled = true;
                gyro.GyroOverride = true;
                OrientationCalculator(gyro);
            }
        }
        public void HoverGyro()
        {
            roll = Convert.ToSingle(right_planetcenter - left_planetcenter) / 10;
            pitch = Convert.ToSingle(back_planetcenter - front_planetcenter) / 10;
            yaw = 0;
        }
        public void TurnLeftRightGyro()
        {
            if(heading.X == 0)
            {
                yaw = Convert.ToSingle(Vector3D.Distance(right_point, target) - Vector3D.Distance(left_point, target)) * 0.3f;
            }
            else
            {
                yaw = Convert.ToSingle(Vector3D.Distance(right_point,heading) - Vector3D.Distance(left_point, heading)) * 0.3f;
            }
            if(yaw > 1) { yaw = 1; }
            else if (yaw < -1) { yaw = -1; }
        }
        public void RotateForwardGyro()
        {
            if(front_planetcenter + 2.5 < back_planetcenter - 2.5) { pitch = 0; } else { pitch = -0.2f; }
        }
        public void RotateBackwardGyro()
        {
            if (front_planetcenter - 2.5 > back_planetcenter + 2.5) { pitch = 0; } else { pitch = 0.2f; }
        }
        public void RotateLeftGyro()
        {
            roll = Convert.ToSingle((left_planetcenter - 2.5) - (right_planetcenter + 2.5)) * 0.03f;
        }
        public void RotateRightGyro()
        {
            roll = Convert.ToSingle((left_planetcenter + 2.5) - (right_planetcenter - 2.5)) * 0.03f;
        }
        public void FlyForwardRotors(IMyMotorAdvancedStator rotor)
        {
            if (rotor.CustomName.Contains("Left"))
            {
                rotor.TargetVelocityRPM = Convert.ToSingle((FrontBackAngle + 0.174533f) * -1 - rotor.Angle) * 100;
            }
            else if (rotor.CustomName.Contains("Right"))
            {
                rotor.TargetVelocityRPM = Convert.ToSingle((FrontBackAngle + 0.174533f) - rotor.Angle) * 100;
            }
        }
        public void FlyBackwardRotors(IMyMotorAdvancedStator rotor)
        {
            if (rotor.CustomName.Contains("Left"))
            {
                rotor.TargetVelocityRPM = Convert.ToSingle((FrontBackAngle - 0.174533f) * -1 - rotor.Angle) * 100;
            }
            else if (rotor.CustomName.Contains("Right"))
            {
                rotor.TargetVelocityRPM = Convert.ToSingle((FrontBackAngle - 0.174533f) - rotor.Angle) * 100;
            }
        }
        public void FlyLeftRotors(IMyMotorAdvancedStator rotor)
        {
            if (rotor.CustomName.Contains("Back"))
            {
                rotor.TargetVelocityRPM = Convert.ToSingle((LeftRightAngle + 0.174533f) * -1 - rotor.Angle) * 100;
            }
            else if (rotor.CustomName.Contains("Front"))
            {
                rotor.TargetVelocityRPM = Convert.ToSingle((LeftRightAngle - 0.174533f) - rotor.Angle) * 100;
            }
        }
        public void FlyRightRotors(IMyMotorAdvancedStator rotor)
        {
            if (rotor.CustomName.Contains("Back"))
            {
                rotor.TargetVelocityRPM = Convert.ToSingle((LeftRightAngle - 0.174533f) * -1 - rotor.Angle) * 100;
            }
            else if (rotor.CustomName.Contains("Front"))
            {
                rotor.TargetVelocityRPM = Convert.ToSingle((LeftRightAngle + 0.174533f) - rotor.Angle) * 100;
            }
        }
        public void HoverLeftRightRotors(IMyMotorAdvancedStator rotor)
        {
            rotor.Enabled = true;
            if (Vector3D.Distance(front_point, last_pos) == Vector3D.Distance(back_point, last_pos))
            {
                if (rotor.CustomName.Contains("Left"))
                {
                    rotor.TargetVelocityRPM = Convert.ToSingle(FrontBackAngle * -1 - rotor.Angle) * 100;
                }
                else if (rotor.CustomName.Contains("Right"))
                {
                    rotor.TargetVelocityRPM = Convert.ToSingle(FrontBackAngle - rotor.Angle) * 100;
                }
            }
            else if (Vector3D.Distance(front_point, last_pos) < Vector3D.Distance(back_point, last_pos))
            {
                FlyForwardRotors(rotor);
            }
            else if (Vector3D.Distance(front_point, last_pos) > Vector3D.Distance(back_point, last_pos))
            {
                FlyBackwardRotors(rotor);
            }
        }
        public void HoverBackFrontRotors(IMyMotorAdvancedStator rotor)
        {
            rotor.Enabled = true;
            if (Vector3D.Distance(left_point, last_pos) == Vector3D.Distance(right_point, last_pos))
            {
                if (rotor.CustomName.Contains("Back"))
                {
                    rotor.TargetVelocityRPM = Convert.ToSingle(LeftRightAngle * -1 - rotor.Angle) * 100;
                }
                else if (rotor.CustomName.Contains("Front"))
                {
                    rotor.TargetVelocityRPM = Convert.ToSingle(LeftRightAngle - rotor.Angle) * 100;
                }
            }
            else if (Vector3D.Distance(left_point, last_pos) < Vector3D.Distance(right_point, last_pos))
            {
                FlyLeftRotors(rotor);
            }
            else if (Vector3D.Distance(left_point, last_pos) > Vector3D.Distance(right_point, last_pos))
            {
                FlyRightRotors(rotor);
            }
        }
        public void StopRotors(IMyMotorAdvancedStator rotor)
        {
            if (Vector3D.Distance(front_point, last_pos)<Vector3D.Distance(back_point,last_pos))
            {
                if (rotor.CustomName.Contains("Left"))
                {
                    rotor.TargetVelocityRPM = Convert.ToSingle((FrontBackAngle + 0.0872665f) * -1 - rotor.Angle) * 100;
                }
                else if (rotor.CustomName.Contains("Right"))
                {
                    rotor.TargetVelocityRPM = Convert.ToSingle((FrontBackAngle + 0.0872665f) - rotor.Angle) * 100;
                }
            }
            else if (Vector3D.Distance(front_point, last_pos) > Vector3D.Distance(back_point, last_pos))
            {
                if (rotor.CustomName.Contains("Left"))
                {
                    rotor.TargetVelocityRPM = Convert.ToSingle((FrontBackAngle - 0.0872665f) * -1 - rotor.Angle) * 100;
                }
                else if (rotor.CustomName.Contains("Right"))
                {
                    rotor.TargetVelocityRPM = Convert.ToSingle((FrontBackAngle - 0.0872665f) - rotor.Angle) * 100;
                }
            }
            if (Vector3D.Distance(left_point, last_pos) > Vector3D.Distance(right_point, last_pos))
            {
                if (rotor.CustomName.Contains("Back"))
                {
                    rotor.TargetVelocityRPM = Convert.ToSingle((LeftRightAngle - 0.0872665f) * -1 - rotor.Angle) * 100;
                }
                else if (rotor.CustomName.Contains("Front"))
                {
                    rotor.TargetVelocityRPM = Convert.ToSingle((LeftRightAngle + 0.0872665f) - rotor.Angle) * 100;
                }
            }
            else if (Vector3D.Distance(left_point, last_pos) > Vector3D.Distance(right_point, last_pos))
            {
                if (rotor.CustomName.Contains("Back"))
                {
                    rotor.TargetVelocityRPM = Convert.ToSingle((LeftRightAngle + 0.0872665f) * -1 - rotor.Angle) * 100;
                }
                else if (rotor.CustomName.Contains("Front"))
                {
                    rotor.TargetVelocityRPM = Convert.ToSingle((LeftRightAngle - 0.0872665f) - rotor.Angle) * 100;
                }
            }
        }
        public void UpThrust(IMyThrust thruster)
        {
            thrustmultiplier = 1.05f;
            thruster.Enabled = true;
            thruster.ThrustOverride = Convert.ToSingle(mass * gravity / thrusters.Count) * thrustmultiplier;
        }
        public void DownThrust(IMyThrust thruster)
        {
            float masstolift = Convert.ToSingle(100 / max_liftweight * mass) * 0.0005f;
            thrustmultiplier = 0.95f + masstolift;
            if(thrustmultiplier > 0.99f) { thrustmultiplier = 0.995f; }
            thruster.Enabled = true;
            thruster.ThrustOverride = Convert.ToSingle(mass * gravity / thrusters.Count) * thrustmultiplier;
        }
        public void HoverThrust(IMyThrust thruster)
        {
            if (altitude > target_altitude)
            {
                DownThrust(thruster);
            }
            else
            {
                thruster.Enabled = true;
                float altitudemultiplier = Convert.ToSingle(target_altitude - altitude) * 0.03f;
                float stabilizer = Convert.ToSingle(last_altitude - altitude);
                thrustmultiplier = 1 + altitudemultiplier + stabilizer;
                thruster.Enabled = true;
                if (headupdown)
                {
                    float thrustercount = ((thrusters.Count - leftrightthrustcount) * 0.5f) + leftrightthrustcount;
                    thruster.ThrustOverride = Convert.ToSingle(mass * gravity / thrustercount) * thrustmultiplier;
                }
                else
                {
                    thruster.ThrustOverride = Convert.ToSingle(mass * gravity / thrusters.Count) * thrustmultiplier;
                }
            }
            if(left_planetcenter-0.5f < right_planetcenter + 0.5f && left_planetcenter + 0.5f > right_planetcenter - 0.5f){ }
            else
            {
                if (thruster.CustomName.Contains("Back"))
                {
                    thruster.ThrustOverride = thruster.ThrustOverride * 1.05f;
                }
            }
        }
        public void Scanning(IMyCameraBlock camera)
        {
            camera.EnableRaycast = true;
            info = camera.Raycast(distance, cpitch, cyaw);
        }
        public void Mining()
        {
            if (cargo_full)
            {
                target_altitude = 15;
                HoverGyro();
                foreach (var thruster in thrusters)
                {
                    HoverThrust(thruster);
                }
                foreach (var rotor in rotors)
                {
                    HoverLeftRightRotors(rotor);
                    HoverBackFrontRotors(rotor);
                }
            }
            else
            {
                MineDown();
            }
            foreach(var drill in drills)
            {
                if (cargo_full == false) { drill.Enabled = true; }
                else { drill.Enabled = false; }
            }
        }
        public void MineUp()
        {
            if (dist_hpforward > 3)
            {
                target_altitude = target_altitude + 0.01f;
            }
            HoverGyro();
            RotateBackwardGyro();
        }
        public void MineDown()
        {
            target_altitude = 3;
            HoverGyro();
            RotateForwardGyro();
            foreach(var thruster in thrusters)
            {
                HoverThrust(thruster);
            }
            foreach(var rotor in rotors)
            {
                if (dist_hpforward > 3 &&altitude<2.8)
                {
                    FlyForwardRotors(rotor);
                    HoverBackFrontRotors(rotor);
                }
                else
                {
                    HoverLeftRightRotors(rotor);
                    HoverBackFrontRotors(rotor);
                }
            }
        }
        public void stopMining()
        {

        }
        public void TestFlight()
        {
            if (test_trigger == false)
            {
                test_start = remote.GetPosition();
                test_trigger = true;
            }
            switch (test_steps)
            {
                case 0:
                    if (home.X != 0)
                    {
                        target = home;
                    }
                    if (Vector3D.Distance(remote.GetPosition(), home) < 15)
                    {
                        test_steps++;
                    }
                    PathFinder();
                    break;
                case 1:
                    target = test_start;
                    if (Vector3D.Distance(remote.GetPosition(), test_start) < 5)
                    {
                        test_steps++;
                    }
                    PathFinder();
                    break;
                case 2:
                    target_altitude = 2.8f;
                    foreach(var thruster in thrusters)
                    {
                        HoverThrust(thruster);
                    }
                    foreach(var rotor in rotors)
                    {
                        HoverLeftRightRotors(rotor);
                        HoverBackFrontRotors(rotor);
                    }
                    HoverGyro();
                    if (altitude <= target_altitude)
                    {
                        test_steps++;
                    }
                    break;
                case 3:
                    foreach(var thrusters in thrusters)
                    {
                        thrusters.Enabled = false;
                    }
                    foreach(var rotor in rotors)
                    {
                        rotor.TargetVelocityRPM = 0;
                    }
                    foreach(var gyro in gyros)
                    {
                        gyro.Pitch = 0;
                        gyro.Yaw = 0;
                        gyro.Roll = 0;
                    }
                    Me.Enabled = false;
                    break;
            }
        } //Fly home
        public void TestFlight1()
        {
            switch (test_steps)
            {
                case 0:
                    if (test_trigger == false)
                    {
                        test_start = remote.GetPosition() + remote.WorldMatrix.Up * 2;
                        target_altitude = Convert.ToSingle(altitude + 2);
                        home = test_start;
                        test_trigger = true;
                    }
                    foreach (var thruster in thrusters)
                    {
                        HoverThrust(thruster);
                    }
                    foreach (var rotor in rotors)
                    {
                        HoverLeftRightRotors(rotor);
                        HoverBackFrontRotors(rotor);
                    }
                    HoverGyro();
                    if (altitude > target_altitude - 2 && altitude < target_altitude + 2) { test_trigger = false; test_steps++; }
                    break;
                case 1:
                    foreach (var thruster in thrusters)
                    {
                        HoverThrust(thruster);
                    }
                    foreach (var rotor in rotors)
                    {
                        HoverLeftRightRotors(rotor);
                        HoverBackFrontRotors(rotor);
                    }
                    HoverGyro();
                    RotateForwardGyro();
                    if (test_timer < 100) { test_timer++; } else { test_steps++; test_timer = 0; }
                    break;
                case 2:
                    Mining();
                    if (cargo_full) { test_steps++; }
                    break;
                case 3:
                    target_altitude = 0;
                    foreach (var thruster in thrusters)
                    {
                        DownThrust(thruster);
                    }
                    foreach (var rotor in rotors)
                    {
                        HoverLeftRightRotors(rotor);
                        HoverBackFrontRotors(rotor);
                    }
                    HoverGyro();
                    if (altitude < 2.5)
                    {
                        test_steps++;
                    }
                    break;
                case 4:
                    foreach (var thruster in thrusters)
                    {
                        thruster.ThrustOverride = 0;
                        thruster.Enabled = false;
                    }
                    foreach (var rotor in rotors)
                    {
                        rotor.TargetVelocityRPM = 0;
                        rotor.Enabled = false;
                    }
                    foreach (var gyro in gyros)
                    {
                        gyro.GyroOverride = false;
                        gyro.Enabled = true;
                    }
                    Me.Enabled = false;
                    break;
            }
        } //Mining test
        public void TestFlight2()
        {
            switch (test_steps)
            {
                case 0:
                    if (test_trigger == false)
                    {
                        test_start = remote.GetPosition() + remote.WorldMatrix.Up * 10;
                        target_altitude = Convert.ToSingle(altitude + 10);
                        home = test_start;
                        test_trigger = true;
                    }
                    foreach (var thruster in thrusters)
                    {
                        HoverThrust(thruster);
                    }
                    foreach (var rotor in rotors)
                    {
                        HoverLeftRightRotors(rotor);
                        HoverBackFrontRotors(rotor);
                    }
                    HoverGyro();
                    if (altitude > target_altitude - 2 && altitude < target_altitude + 2) { test_trigger = false; test_steps++; }
                    break;
                case 1:
                    if (test_trigger == false)
                    {
                        test_point = remote.GetPosition() + remote.WorldMatrix.Left * 50; ;
                        target = test_point;
                        test_trigger = true;
                    }
                    foreach (var thruster in thrusters)
                    {
                        HoverThrust(thruster);
                    }
                    foreach (var rotor in rotors)
                    {
                        HoverLeftRightRotors(rotor);
                        HoverBackFrontRotors(rotor);
                    }
                    HoverGyro();
                    if (test_point.X != 0)
                    {
                        TurnLeftRightGyro();
                        if (
                            Vector3D.Distance(left_point, test_point) - 0.1 < Vector3D.Distance(right_point, test_point) + 0.1 && Vector3D.Distance(left_point, test_point) + 0.1 > Vector3D.Distance(right_point, test_point) - 0.1
                            && altitude > target_altitude - 2 && altitude < target_altitude + 2
                            )
                        {
                            test_trigger = false; test_steps++;
                        }
                    }
                    break;
                case 2:
                    if (test_trigger == false)
                    {
                        test_point = remote.GetPosition() + remote.WorldMatrix.Right * 50; ;
                        target = test_point;
                        test_trigger = true;
                    }
                    foreach (var thruster in thrusters)
                    {
                        HoverThrust(thruster);
                    }
                    foreach (var rotor in rotors)
                    {
                        
                        if (VelocityForward == VelocityBackward)
                        {
                            HoverBackFrontRotors(rotor);
                        }
                        else if (VelocityBackward > VelocityForward)
                        {
                            FlyBackwardRotors(rotor);
                        }
                        else if (VelocityForward > VelocityBackward)
                        {
                            FlyForwardRotors(rotor);
                        }
                    }
                    HoverGyro();
                    if (test_point.X != 0)
                    {
                        TurnLeftRightGyro();
                        if (
                            Vector3D.Distance(left_point, test_point) - 0.1 < Vector3D.Distance(right_point, test_point) + 0.1 && Vector3D.Distance(left_point, test_point) + 0.1 > Vector3D.Distance(right_point, test_point) - 0.1
                            && altitude > target_altitude - 2 && altitude < target_altitude + 2
                            )
                        {
                            test_trigger = false; test_steps++;
                        }
                    }
                    break;
                case 3:
                    foreach (var thruster in thrusters)
                    {
                        HoverThrust(thruster);
                    }
                    foreach (var rotor in rotors)
                    {
                        HoverLeftRightRotors(rotor);
                        HoverBackFrontRotors(rotor);
                    }
                    HoverGyro();
                    RotateForwardGyro();
                    ControlGyro();
                    if (front_planetcenter + 1 < back_planetcenter - 1) { test_steps++; }
                    break;
                case 4:
                    if (Vector3D.Distance(remote.GetPosition(), test_start) > 6)
                    {
                        target = test_start;
                        PathFinder();
                    }
                    else
                    {
                        foreach (var thruster in thrusters)
                        {
                            HoverThrust(thruster);
                        }
                        foreach (var rotor in rotors)
                        {
                            HoverLeftRightRotors(rotor);
                            HoverBackFrontRotors(rotor);
                        }
                        HoverGyro();

                        if (front_planetcenter - 0.2 < back_planetcenter + 0.2 && front_planetcenter + 0.2 > back_planetcenter - 0.2 && altitude > target_altitude - 2 && altitude < target_altitude + 2 && speed < 1)
                        { test_steps++; }
                    }
                    break;
                case 5:
                    foreach (var thruster in thrusters)
                    {
                        HoverThrust(thruster);
                    }
                    foreach (var rotor in rotors)
                    {
                        HoverLeftRightRotors(rotor);
                        HoverBackFrontRotors(rotor);
                    }
                    HoverGyro();
                    RotateBackwardGyro();
                    if (front_planetcenter - 1 > back_planetcenter + 1) { test_steps++; }
                    break;
                case 6:
                    if (Vector3D.Distance(remote.GetPosition(), test_start) > 6)
                    {
                        target = test_start;
                        PathFinder();
                    }
                    else
                    {
                        foreach (var thruster in thrusters)
                        {
                            HoverThrust(thruster);
                        }
                        foreach (var rotor in rotors)
                        {
                            HoverLeftRightRotors(rotor);
                            HoverBackFrontRotors(rotor);
                        }
                        HoverGyro();
                        if (front_planetcenter - 0.2 < back_planetcenter + 0.2 && front_planetcenter + 0.2 > back_planetcenter - 0.2 && altitude > target_altitude - 2 && altitude < target_altitude + 2 && speed < 1)
                        { test_steps++; }
                    }
                    break;
                case 7:
                    foreach (var thruster in thrusters)
                    {
                        HoverThrust(thruster);
                    }
                    foreach (var rotor in rotors)
                    {
                        HoverLeftRightRotors(rotor);
                        HoverBackFrontRotors(rotor);
                    }
                    HoverGyro();
                    RotateLeftGyro();
                    if (left_planetcenter + 1 < right_planetcenter - 1) { test_steps++; }
                    break;
                case 8:
                    if (Vector3D.Distance(remote.GetPosition(), test_start) > 6)
                    {
                        target = test_start;
                        PathFinder();
                    }
                    else
                    {
                        foreach (var thruster in thrusters)
                        {
                            HoverThrust(thruster);
                        }
                        foreach (var rotor in rotors)
                        {
                            HoverLeftRightRotors(rotor);
                            HoverBackFrontRotors(rotor);
                        }
                        HoverGyro();
                        if (left_planetcenter - 0.02 < right_planetcenter + 0.02 && left_planetcenter + 0.02 > right_planetcenter - 0.02 && altitude > target_altitude - 2 && altitude < target_altitude + 2 && speed < 1)
                        { test_steps++; }
                    };
                    break;
                case 9:
                    foreach (var thruster in thrusters)
                    {
                        HoverThrust(thruster);
                    }
                    foreach (var rotor in rotors)
                    {
                        HoverLeftRightRotors(rotor);
                        HoverBackFrontRotors(rotor);
                    }
                    HoverGyro();
                    RotateRightGyro();
                    if (left_planetcenter - 1 > right_planetcenter + 1) { test_steps++; }
                    break;
                case 10:
                    if (Vector3D.Distance(remote.GetPosition(), test_start) > 6)
                    {
                        target = test_start;
                        PathFinder();
                    }
                    else
                    {
                        foreach (var thruster in thrusters)
                        {
                            HoverThrust(thruster);
                        }
                        foreach (var rotor in rotors)
                        {
                            HoverLeftRightRotors(rotor);
                            HoverBackFrontRotors(rotor);
                        }
                        HoverGyro();
                        if (left_planetcenter - 0.02 < right_planetcenter + 0.02 && left_planetcenter + 0.02 > right_planetcenter - 0.02 && altitude > target_altitude - 2 && altitude < target_altitude + 2 && speed < 1)
                        { test_steps++; }
                    };
                    break;
                case 11:
                    if (test_trigger == false)
                    {
                        target = remote.CenterOfMass + remote.WorldMatrix.Backward * 150;
                        test_trigger = true;
                    }
                    PathFinder();
                    if (Vector3D.Distance(remote.CenterOfMass, target) < 15) { test_trigger = false; test_steps++; }
                    break;
                case 12:
                    if (test_trigger == false)
                    {
                        target = test_start;
                        test_trigger = true;
                    }
                    PathFinder();
                    if (Vector3D.Distance(remote.CenterOfMass, target) < 6) { test_trigger = false; test_steps++; }
                    break;
                case 13:
                    if (test_trigger == false)
                    {
                        temp_test_Target = remote.CenterOfMass + remote.WorldMatrix.Backward * 50;
                        heading = temp_test_Target + (temp_test_Target - test_start) * 100;
                        test_trigger = true;
                    }
                    foreach (var thruster in thrusters)
                    {
                        HoverThrust(thruster);
                    }
                    foreach (var rotor in rotors)
                    {
                        HoverLeftRightRotors(rotor);
                        HoverBackFrontRotors(rotor);
                    }
                    HoverGyro();
                    TurnLeftRightGyro();
                    if (Vector3D.Distance(front_point, heading) < Vector3D.Distance(back_point, heading))
                    {
                        if (Vector3D.Distance(left_point, heading) - 0.1 < Vector3D.Distance(right_point, heading) + 0.1 && Vector3D.Distance(left_point, heading) + 0.1 > Vector3D.Distance(right_point, heading) - 0.1)
                        {
                            test_trigger = false; test_steps++;
                        }
                    }
                    break;
                case 14:
                    target_altitude = 0;
                    foreach (var thruster in thrusters)
                    {
                        DownThrust(thruster);
                    }
                    foreach (var rotor in rotors)
                    {
                        HoverLeftRightRotors(rotor);
                        HoverBackFrontRotors(rotor);
                    }
                    HoverGyro();
                    if (altitude < 2.5)
                    {
                        test_steps++;
                    }
                    break;
                case 15:
                    foreach (var thruster in thrusters)
                    {
                        thruster.ThrustOverride = 0;
                        thruster.Enabled = false;
                    }
                    foreach (var rotor in rotors)
                    {
                        rotor.TargetVelocityRPM = 0;
                        rotor.Enabled = false;
                    }
                    foreach (var gyro in gyros)
                    {
                        gyro.GyroOverride = false;
                        gyro.Enabled = true;
                    }
                    Me.Enabled = false;
                    break;
            }
        } // Movement test
    }
}

﻿using System;
using System.Drawing;
using System.Windows.Forms;
using BulletSharp;
using DemoFramework;
using SlimDX;
using SlimDX.Direct3D9;

namespace VehicleDemo
{
    public class VehicleDemo : Game
    {
        Vector3 eye = new Vector3(5, 15, 5);
        Vector3 target = Vector3.Zero;
        int ViewMode = 1;

        Light light;
        Material bodyMaterial, wheelMaterial;
        Mesh wheel;
        public Mesh ground;

        Physics Physics
        {
            get { return (Physics)PhysicsContext; }
            set { PhysicsContext = value; }
        }

        protected override void OnInitializeDevice()
        {
            Form.Text = "BulletSharp - Vehicle Demo";
            base.OnInitializeDevice();
        }

        protected override void OnInitialize()
        {
            Physics = new Physics(this);

            wheel = Mesh.CreateCylinder(Device, Physics.wheelRadius, Physics.wheelRadius, Physics.wheelWidth, 10, 10);
            ground.ComputeNormals();

            Ambient = Color.Black.ToArgb();

            light = new Light();
            light.Type = LightType.Point;
            light.Range = 500;
            light.Position = new Vector3(0, 100, 0);
            light.Direction = -Vector3.UnitY;
            light.Diffuse = Color.White;
            light.Attenuation0 = 0.75f;

            bodyMaterial = new Material();
            bodyMaterial.Diffuse = Color.Blue;
            bodyMaterial.Ambient = new Color4(Ambient);

            wheelMaterial = new Material();
            wheelMaterial.Diffuse = Color.Red;
            wheelMaterial.Ambient = new Color4(Ambient);

            Fps.Text = "F2 - Toggle view mode: Tracking\n" +
                "F3 - Toggle debug\n" +
                "F11 - Toggle fullscreen";

            FarPlane = 600f;
            Freelook.SetEyeTarget(new Vector3(30, 10, 0), target);

            base.OnInitialize();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                wheel.Dispose();
                ground.Dispose();
            }
        }

        protected override void OnResourceLoad()
        {
            base.OnResourceLoad();

            Device.SetLight(0, light);
            Device.EnableLight(0, true);

            Device.SetRenderState(RenderState.CullMode, Cull.None);

            Device.SetRenderState(RenderState.FogEnable, true);
            Device.SetRenderState(RenderState.FogStart, 125f);
            Device.SetRenderState(RenderState.FogEnd, 400f);
            Device.SetRenderState(RenderState.FogColor, Color.LightGray.ToArgb());
            Device.SetRenderState(RenderState.FogTableMode, FogMode.Linear);
        }

        protected override void OnHandleInput()
        {
            // Can only shoot boxes in FreeLook mode
            if (Input.KeysPressed.Contains(Keys.Space))
            {
                if (ViewMode != 3)
                {
                    Input.KeysPressed.Remove(Keys.Space);
                }
            }

            if (Input.KeysPressed.Contains(Keys.F2))
            {
                ViewMode++;
                if (ViewMode > 3) ViewMode = 1;

                string viewMode;
                switch (ViewMode)
                {
                    case 1:
                        viewMode = "Tracking";
                        break;
                    case 2:
                        viewMode = "Following";
                        break;
                    case 3:
                        viewMode = "Freelook (WASD + mouse)";
                        break;
                    default:
                        viewMode = "";
                        break;
                }
                Fps.Text = "F2 - Toggle view mode: " + viewMode + "\n" +
                    "F3 - Toggle debug\n" +
                    "F11 - Toggle fullscreen";
            }

            Physics.HandleKeys(Input, FrameDelta);

            base.OnHandleInput();
        }

        protected override void OnRender()
        {
            Device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.LightGray, 1.0f, 0);
            Device.BeginScene();

            Matrix trans = Physics.vehicle.ChassisWorldTransform;
            Vector4 pos = trans.get_Rows(3);
            Vector3 pos2 = new Vector3(pos.X, pos.Y, pos.Z);

            // Set View matrix based on view mode
            if (ViewMode == 1)
            {
                Matrix view = Matrix.LookAtLH(new Vector3(30, 20, 30), pos2, Vector3.UnitY);
                Device.SetTransform(TransformState.View, view);
            }
            else if (ViewMode == 2)
            {
                Freelook.SetEyeTarget(pos2 + new Vector3(30, 10, 0), pos2);
                Device.SetTransform(TransformState.View, Freelook.View);
            }
            else if (ViewMode == 3)
            {
                Device.SetTransform(TransformState.View, Freelook.View);
            }

            // Draw wireframe
            /*
            if (IsDebugDrawEnabled == false)
            {
                Device.SetRenderState(RenderState.Lighting, false);
                Device.SetRenderState(RenderState.FillMode, FillMode.Wireframe);
                Device.SetTransform(TransformState.World, Matrix.Identity);
                ground.DrawSubset(0);
                Device.SetRenderState(RenderState.Lighting, true);
                Device.SetRenderState(RenderState.FillMode, FillMode.Solid);
            }
            */

            Device.Material = wheelMaterial;
            int i;
            for (i = 0; i < Physics.vehicle.NumWheels; i++)
            {
                //synchronize the wheels with the (interpolated) chassis worldtransform
                Physics.vehicle.UpdateWheelTransform(i, true);
                //draw wheels (cylinders)
                trans = Matrix.RotationY((float)Math.PI / 2);
                trans *= Physics.vehicle.GetWheelInfo(i).WorldTransform;
                Device.SetTransform(TransformState.World, trans);
                wheel.DrawSubset(0);
            }

            DebugDrawWorld();

            foreach (CollisionObject colObj in Physics.World.CollisionObjectArray)
            {
                RigidBody body = RigidBody.Upcast(colObj);
                if ((string)body.UserObject == "Ground")
                {
                    Device.SetTransform(TransformState.World, Matrix.Identity);
                    Device.Material = GroundMaterial;
                    ground.DrawSubset(0);
                    continue;
                }
                else if ((string)body.UserObject == "Chassis")
                    Device.Material = bodyMaterial;
                else if (colObj.ActivationState == ActivationState.ActiveTag)
                    Device.Material = ActiveMaterial;
                else
                    Device.Material = PassiveMaterial;
                Device.SetTransform(TransformState.World, body.MotionState.WorldTransform);
                MeshFactory.Render(colObj);
            }

            Fps.OnRender(FramesPerSecond);

            Device.EndScene();
            Device.Present();
        }

        public Device Device
        {
            get { return Device9; }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            VehicleDemo game = new VehicleDemo();

            if (game.TestLibraries() == false)
                return;

            game.Run();
            game.Dispose();
        }
    }
}

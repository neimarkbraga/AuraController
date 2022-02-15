using System;
using System.Collections.Generic;
using System.Text.Json;
using AuraServiceLib;

namespace AuraController
{
    struct RequestData
    {
        public List<uint> Lights { get; set; }
        public uint Color { get; set; }
    }

    struct AuraDeviceLight
    {
        public uint Color { get; set; }
        public string Name { get; set; }
        public uint Red { get; set; }
        public uint Green { get; set; }
        public uint Blue { get; set; }
    }

    struct AuraDevice {
        public uint Type { get; set; }
        public string Name { get; set; }
        public uint Height { get; set; }
        public uint Width { get; set; }
        public List<AuraDeviceLight> Lights { get; set; }
    }

    class Program
    {
        static bool active = false;
        static IAuraSdk2 sdk = (IAuraSdk2)new AuraSdk();
        static HttpServer server = new HttpServer();

        static IAuraSyncDeviceCollection cache_devices = null; 

        static void RequireActiveControl()
        {
            if (!active)
                throw new Exception("Aura control is required to be active to proceeed with the action.");
        }

        static IAuraSyncDeviceCollection GetCachedDevices()
        {
            if (cache_devices == null) cache_devices = sdk.Enumerate(0);
            return cache_devices;
        }

        static List<AuraDevice> GetDevices()
        {   
            IAuraSyncDeviceCollection devices = GetCachedDevices();

            List<AuraDevice> _devices = new List<AuraDevice>();
            foreach (IAuraSyncDevice device in devices)
            {
                AuraDevice _device = new AuraDevice()
                {
                    Type = device.Type,
                    Name = device.Name,
                    Height = device.Height,
                    Width = device.Width,
                    Lights = new List<AuraDeviceLight>(),
                };

                foreach (IAuraRgbLight light in device.Lights)
                {
                    _device.Lights.Add(new AuraDeviceLight()
                    {
                        Color = light.Color,
                        Name = light.Name,
                        Red = light.Red,
                        Green = light.Green,
                        Blue = light.Blue
                    });
                }
                _devices.Add(_device);
            }

            return _devices;
        }

        static void Main(string[] args)
        {
            // returns if server is in control of aura
            server.Get("/active", (req, res, next) =>
            {  
                HttpServer.SendJson(new { active }, res);
            });

            // activate control; acquire control to aura
            server.Post("/activate", (req, res, next) =>
            {
                if (!active)
                {
                    sdk.SwitchMode();
                    active = true;
                }

                HttpServer.SendJson(new {
                    message = "activated successfully"
                }, res);
            });

            // deactivate control; give up control to aura
            server.Post("/deactivate", (req, res, next) =>
            {
                if (active)
                {
                    sdk.ReleaseControl(0);
                    active = false;
                }

                HttpServer.SendJson(new
                {
                    message = "deactivated successfully"
                }, res);
            });
            
            // returns available devices
            server.Get("/devices", (req, res, next) =>
            {
                RequireActiveControl();
                HttpServer.SendJson(GetDevices(), res);
            });

            // set device lights color
            server.Put("/devices/:device_name", (req, res, next) =>
            {
                RequireActiveControl();

                Dictionary<string, string> param = HttpServer.GetParams(req);
                string body = HttpServer.GetBodyContent(req);
                RequestData config = JsonSerializer.Deserialize<RequestData>(body);
                foreach (IAuraSyncDevice device in GetCachedDevices())
                {
                    if (device.Name == param["device_name"])
                    {
                        for (uint i = 0; i < device.Lights.Count; i++)
                            if (config.Lights == null || config.Lights.Contains(i))
                                device.Lights[(int)i].Color = config.Color;
                        device.Apply();
                        break;
                    }
                }

                HttpServer.SendJson(GetDevices().Find(a => a.Name == param["device_name"]), res);
            });

            Console.WriteLine("Listening to port 8080");
            server.Listen(8080);
        }
    }
}
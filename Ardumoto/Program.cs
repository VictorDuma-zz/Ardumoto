using System;
using System.Threading;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.Pwm;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Networking.SPWF04Sx;
using GHIElectronics.TinyCLR.Devices.Spi;
using System.Text;
using System.Diagnostics;

namespace Ardumoto {
    class Program {
        private static GpioPin btn1;
        private static GpioPin led1;
        private static SPWF04SxInterface wifi;

        public static PwmController PWM1 = PwmController.FromId(FEZ.PwmPin.Controller1.Id);
        public static PwmController PWM3 = PwmController.FromId(FEZ.PwmPin.Controller3.Id);

        public static PwmPin PWMA = PWM1.OpenPin(FEZ.PwmPin.Controller1.D3);
        public static PwmPin PWMB = PWM3.OpenPin(FEZ.PwmPin.Controller3.D11);

        public static GpioPin DIRA;
        public static GpioPin DIRB;

        public enum Servo {
            A,
            B
        }

        public enum Direction {
            Forvard,
            Back
        }

        static void Main() {
            var buffer = new byte[512];

            DIRA = GpioController.GetDefault().OpenPin(FEZ.GpioPin.D2);
            DIRA.SetDriveMode(GpioPinDriveMode.Output);

            DIRB = GpioController.GetDefault().OpenPin(FEZ.GpioPin.D4);
            DIRB.SetDriveMode(GpioPinDriveMode.Output);
            var cont = GpioController.GetDefault();

            var reset = cont.OpenPin(FEZ.GpioPin.WiFiReset);
            var irq = cont.OpenPin(FEZ.GpioPin.WiFiInterrupt);
            var spi = SpiDevice.FromId(FEZ.SpiBus.WiFi, SPWF04SxInterface.GetConnectionSettings(FEZ.GpioPin.WiFiChipSelect));
            led1 = cont.OpenPin(FEZ.GpioPin.Led1);
            btn1 = cont.OpenPin(FEZ.GpioPin.Btn1);

            led1.SetDriveMode(GpioPinDriveMode.Output);
            btn1.SetDriveMode(GpioPinDriveMode.InputPullUp);

            wifi = new SPWF04SxInterface(spi, irq, reset);
            wifi.TurnOn();
            //wifi.JoinNetwork("GHI", "ghi555wifi.");

            Debug.WriteLine("WaitForButton");
            WaitForButton();

            var speed = 90;
            var id = wifi.OpenSocket("192.168.1.152", 80, SPWF04SxConnectionyType.Tcp, SPWF04SxConnectionSecurityType.None);

            StringBuilder builder = new StringBuilder();

            while (true) {
                if (wifi.QuerySocket(id) is var avail && avail > 0) {
                    wifi.ReadSocket(id, buffer, 0, Math.Min(avail, buffer.Length));

                    for (var k = 0; k < buffer.Length; k++) {
                        if (buffer[k] != 0) {
                            char result = (char)buffer[k];
                            builder.Append(result);
                            buffer[k] = 0;
                        }
                    }
                    Debug.WriteLine(builder.ToString());
                }
                string command = builder.ToString();
                builder.Clear();

                switch (command) {
                    case "forvard":
                        SetMotorDuty(Servo.A, speed, Direction.Forvard);
                        SetMotorDuty(Servo.B, speed, Direction.Forvard);
                        break;
                    case "backward":
                        SetMotorDuty(Servo.A, speed, Direction.Back);
                        SetMotorDuty(Servo.B, speed, Direction.Back);
                        break;
                    case "left":
                        SetMotorDuty(Servo.A, speed, Direction.Forvard);
                        StopMotor(Servo.B);
                        Thread.Sleep(500);
                        break;
                    case "right":
                        SetMotorDuty(Servo.B, speed, Direction.Forvard);
                        StopMotor(Servo.A);
                        Thread.Sleep(500);
                        break;
                    case "stop":
                        StopMotor(Servo.B);
                        StopMotor(Servo.A);
                        Thread.Sleep(500);
                        break;
                    default:
                        break;
                }
                Thread.Sleep(100);
            }
        }

        private static void WaitForButton() {
            while (btn1.Read() == GpioPinValue.High) {
                led1.Write(led1.Read() == GpioPinValue.High ? GpioPinValue.Low : GpioPinValue.High);
                Thread.Sleep(50);
            }

            while (btn1.Read() == GpioPinValue.Low)
                Thread.Sleep(50);
        }

        public static void StopMotor(Servo servo) {
            if (servo == Servo.A)
                PWMA.Stop();
            else
                PWMB.Stop();
        }

        public static void SetMotorDuty(Servo servo, double speedPercentage, Direction direction) {
            if (speedPercentage < 0 || speedPercentage > 100) throw new ArgumentOutOfRangeException("speedPercentage", "Must be between 0 and 100 %");
            if (servo == Servo.A) {
                if (direction == Direction.Back)
                    DIRA.Write(GpioPinValue.Low);
                else
                    DIRA.Write(GpioPinValue.High);
                PWM1.SetDesiredFrequency(5000);
                PWMA.Start();
                PWMA.SetActiveDutyCyclePercentage(speedPercentage / 100);
            }
            else {
                if (direction == Direction.Forvard)
                    DIRB.Write(GpioPinValue.High);
                else
                    DIRB.Write(GpioPinValue.Low);
                PWM3.SetDesiredFrequency(5000);
                PWMB.Start();
                PWMB.SetActiveDutyCyclePercentage(speedPercentage / 100);
            }
        }
    }
}


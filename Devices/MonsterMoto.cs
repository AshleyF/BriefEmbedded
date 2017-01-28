using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Robotics;
using Microsoft.Robotics.Microcontroller;
using Microsoft.Robotics.Numerics;
using System.Linq;

namespace Sparkfun.MonsterMoto
{
    // Monster Moto Shield from Sparkpun Electronics
    // http://www.sparkfun.com/products/10182

    public class Motor : IDcMotor
    {
        private readonly IMicrocontroller mcu;
        private readonly int pinA, pinB, pwmPin;
        private readonly string name;

        public Motor(IMicrocontroller mcu, int pinA, int pinB, int pwmPin, string name, double smoothing = double.MaxValue)
        {
            this.mcu = mcu;
            this.pinA = pinA;
            this.pinB = pinB;
            this.pwmPin = pwmPin;
            this.name = name;
            this.smoothing = smoothing;
        }

        public async Task Initialize()
        {
            mcu.DefineBrief("motorPins", "{0} {1} {2}", pinA, pinB, pwmPin);
            mcu.DefineBrief("motorDrive", "dup 0 < [neg high low] [low high] choice push push swap analogWrite pop swap digitalWrite pop swap digitalWrite"); // ( pins level -- )
            mcu.DefineBrief("motorStop", "0 swap analogWrite high swap digitalWrite high swap digitalWrite"); // ( pins -- ) \ always powered braking

            await mcu.ExecuteBrief("motorPins output pinMode output pinMode output pinMode"); // init pins
            await Brake();
        }

        private double targetPwmPercentage = 0;

        /// <summary>
        /// Gets the target pwm percentage to the HBridge
        /// </summary>
        public double TargetPwmPercentage
        {
            get { return targetPwmPercentage; }
        }

        private double currentPwmPercentage = 0; // TODO: do this at the MCU

        /// <summary>
        /// Gets the current pwm percentage to the HBridge
        /// </summary>
        public double CurrentPwmPercentage
        {
            // TODO: async from MCU
            get { return currentPwmPercentage; }
        }

        private double smoothing = 1;

        /// <summary>
        /// Gets the smoothing in maximum percent change per second
        /// </summary>
        public double Smoothing
        {
            get { return smoothing; }
        }

        /// <summary>
        /// Set the target PWM percentage to the HBridge
        /// </summary>
        /// <param name="percent">Duty cycle as a percent (0.0 to 100.0) with sign indicating direction</param>
        /// <returns>Task completes when the target power is reached</returns>
        public async Task SetTargetPwmPercentage(double percent)
        {
            // TODO: send to MCU
            await SetTargetPwmPercentage(percent, CancellationToken.None);
        }

        /// <summary>
        /// Set the target PWM percentage to the HBridge
        /// </summary>
        /// <param name="percent">Duty cycle as a percent (0.0 to 100.0) with sign indicating direction</param>
        /// <param name="cancellationToken">Cancellation Token to transition from drive to coast</param>
        /// <returns>Task completes when the target power is reached</returns>
        public async Task SetTargetPwmPercentage(double percent, CancellationToken cancellationToken)
        {
            if (percent < -1 || percent > 1)
                throw new ArgumentOutOfRangeException();
            
            // TODO: do this at the MCU!
            targetPwmPercentage = percent;
            
            const int millisecondsPerIncrement = 100;
            double smoothingPerIncrement = smoothing / (1000 / millisecondsPerIncrement);
            var signedDiff = targetPwmPercentage - currentPwmPercentage;
            var sign = Math.Sign(signedDiff);
            var diff = Math.Abs(signedDiff);

            while (diff > double.Epsilon && !cancellationToken.IsCancellationRequested)
            {
                currentPwmPercentage += Math.Min(diff, smoothingPerIncrement) * sign;
                await mcu.ExecuteBrief("motorPins {0} motorDrive", (int)(currentPwmPercentage * 255 + 0.5));
                diff = Math.Abs(currentPwmPercentage - targetPwmPercentage);
                if (diff > double.Epsilon)
                    await Task.Delay(millisecondsPerIncrement, cancellationToken);
            }
        }

        /// <summary>
        /// Set the maximum percent change per second
        /// </summary>
        /// <param name="percentPerSecond">Percent change per second</param>
        /// <returns>Task completes when microcontroller acknowledges the command</returns>
        public Task SetSmoothing(double percentPerSecond)
        {
            // TODO: send to MCU
            // TODO: why is this async if done PC-side? (APIs above are inconsistent if MCU-side expected)
            return Task.Factory.StartNew(() => smoothing = percentPerSecond);
        }

        /// <summary>
        /// Actively brake the motors for an abrupt emergency stop.
        /// </summary>
        /// <returns>Task completes when microcontroller acknowledges the command</returns>
        public async Task Brake()
        {
            await mcu.ExecuteBrief("motorPins motorStop");
        }


        Task<double> IDcMotor.CurrentPwmPercentage
        {
            get { throw new NotImplementedException(); }
        }

        public string Id
        {
            get { throw new NotImplementedException(); }
        }

        public Pose PlacementPose
        {
            get { throw new NotImplementedException(); }
        }
    }

#region RDK

    // TODO: Remove these!
    //       These are forked from private\src\RDK\Microsoft.Robotics\Devices

    /// <summary>
    /// Base interface for all actuators
    /// </summary>
    public interface IActuator
    {
        /// <summary>
        /// Gets Unique Device Id
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the physical placement of the device as a pose that includes position and orientation
        /// </summary>
        Pose PlacementPose { get; }
    }
    /// <summary>
    /// IDcMotor is used to control a DC motor by setting pwm percentage or active braking.
    /// Acceleration can be set to smooth the power changes.
    /// </summary>
    public interface IDcMotor : IActuator
    {
        /// <summary>
        /// Gets the target pwm percentage to the HBridge
        /// </summary>
        double TargetPwmPercentage { get; }

        /// <summary>
        /// Gets the current pwm percentage to the HBridge
        /// </summary>
        Task<double> CurrentPwmPercentage { get; }

        /// <summary>
        /// Gets the smoothing in maximum percent change per second
        /// </summary>
        double Smoothing { get; }

        /// <summary>
        /// Initialize the DcMotor.
        /// </summary>
        /// <returns>Task completes when initialized</returns>
        Task Initialize();

        /// <summary>
        /// Set the target PWM percentage to the HBridge
        /// </summary>
        /// <param name="targetPwmPercentage">Duty cycle as a percent (0.0 to 100.0) with sign indicating direction</param>
        /// <returns>Task completes when the target power is reached</returns>
        Task SetTargetPwmPercentage(double targetPwmPercentage);

        /// <summary>
        /// Set the maximum percent change per second
        /// </summary>
        /// <param name="percentChangePerSecond">Percent change per second</param>
        /// <returns>Task completes when microcontroller acknowledges the command</returns>
        Task SetSmoothing(double percentChangePerSecond);

        /// <summary>
        /// Actively brake the motors for an abrupt emergency stop.
        /// </summary>
        /// <returns>Task completes when microcontroller acknowledges the command</returns>
        Task Brake();
    }

#endregion
}
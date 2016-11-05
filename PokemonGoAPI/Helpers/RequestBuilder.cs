using System;
using System.Linq;
using Google.Protobuf;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Extensions;
using POGOProtos.Networking.Envelopes;
using POGOProtos.Networking.Requests;
using static POGOProtos.Networking.Envelopes.RequestEnvelope.Types;

namespace PokemonGo.RocketAPI.Helpers
{
    public class RequestBuilder
    {
        private readonly int _accuracy;
        private readonly AuthTicket _authTicket;
        private readonly string _authToken;
        private readonly AuthType _authType;
        private readonly IDeviceInfo _deviceInfo;
        private readonly double _latitude;
        private readonly double _longitude;
        private readonly Random _random = new Random();
        private static byte[] _sessionHash = null;

        public RequestBuilder(string authToken, AuthType authType, double latitude, double longitude, int accuracy,
            IDeviceInfo deviceInfo,
            AuthTicket authTicket = null)
        {
            _authToken = authToken;
            _authType = authType;
            _latitude = latitude;
            _longitude = longitude;
            _accuracy = accuracy;
            _authTicket = authTicket;
            _deviceInfo = deviceInfo;
        }

        public RequestEnvelope SetRequestEnvelopeUnknown6(RequestEnvelope requestEnvelope)
        {
            if(_sessionHash == null)
            {
                _sessionHash = new byte[32];
                _random.NextBytes(_sessionHash);
            }

            byte[] authSeed = requestEnvelope.AuthTicket != null ?
                requestEnvelope.AuthTicket.ToByteArray() :
                requestEnvelope.AuthInfo.ToByteArray();


            var normAccel = new Vector(_deviceInfo.Sensors.AccelRawX, _deviceInfo.Sensors.AccelRawY, _deviceInfo.Sensors.AccelRawZ);
            normAccel.NormalizeVector(1.0f);//1.0f on iOS, 9.81 on Android?

            var sig = new Signature
            {
                LocationHash1 = (int)
                    Utils.GenerateLocation1(authSeed, requestEnvelope.Latitude, requestEnvelope.Longitude,
                        requestEnvelope.Accuracy, _deviceInfo.VersionData.HashSeed1),
                LocationHash2 = (int)
                    Utils.GenerateLocation2(requestEnvelope.Latitude, requestEnvelope.Longitude,
                        requestEnvelope.Accuracy, _deviceInfo.VersionData.HashSeed1),
                SessionHash = ByteString.CopyFrom(_sessionHash),
                Unknown25 = _deviceInfo.VersionData.VersionHash,
                Timestamp = (ulong)DateTime.UtcNow.ToUnixTime(),
                TimestampSinceStart = (ulong)_deviceInfo.TimeSnapshot,

                DeviceInfo = new Signature.Types.DeviceInfo
                {
                    DeviceId = _deviceInfo.DeviceID,
                    AndroidBoardName = _deviceInfo.AndroidBoardName,
                    AndroidBootloader = _deviceInfo.AndroidBootloader,
                    DeviceBrand = _deviceInfo.DeviceBrand,
                    DeviceModel = _deviceInfo.DeviceModel,
                    DeviceModelBoot = _deviceInfo.DeviceModelBoot,
                    DeviceModelIdentifier = _deviceInfo.DeviceModelIdentifier,
                    FirmwareFingerprint = _deviceInfo.FirmwareFingerprint,
                    FirmwareTags = _deviceInfo.FirmwareTags,
                    HardwareManufacturer = _deviceInfo.HardwareManufacturer,
                    HardwareModel = _deviceInfo.HardwareModel,
                    FirmwareBrand = _deviceInfo.FirmwareBrand,
                    FirmwareType = _deviceInfo.FirmwareType
                },

                ActivityStatus = _deviceInfo.ActivityStatus != null ? new Signature.Types.ActivityStatus()
                {
                    Walking = _deviceInfo.ActivityStatus.Walking,
                    Automotive = _deviceInfo.ActivityStatus.Automotive,
                    Cycling = _deviceInfo.ActivityStatus.Cycling,
                    Running = _deviceInfo.ActivityStatus.Running,
                    Stationary = _deviceInfo.ActivityStatus.Stationary,
                    Tilting = _deviceInfo.ActivityStatus.Tilting,
                }
                : null
            };


            if(_deviceInfo.GpsSattelitesInfo.Length > 0)
            {
                //sig.GpsInfo.TimeToFix //currently not filled

                _deviceInfo.GpsSattelitesInfo.ToList().ForEach(sat =>
                {
                    Signature.Types.AndroidGpsInfo gpsInfo = new Signature.Types.AndroidGpsInfo();

                    gpsInfo.Azimuth.Add(sat.Azimuth);
                    gpsInfo.Elevation.Add(sat.Elevation);
                    gpsInfo.HasAlmanac.Add(sat.Almanac);
                    gpsInfo.HasEphemeris.Add(sat.Emphasis);
                    gpsInfo.SatellitesPrn.Add(sat.SattelitesPrn);
                    gpsInfo.Snr.Add(sat.Snr);
                    gpsInfo.UsedInFix.Add(sat.UsedInFix);

                    sig.GpsInfo.Add(gpsInfo);
                });
            }

            _deviceInfo.LocationFixes.ToList().ForEach(loc => sig.LocationFix.Add(new Signature.Types.LocationFix
            {
                Floor = loc.Floor,
                Longitude = loc.Longitude,
                Latitude = loc.Latitude,
                Altitude = loc.Altitude,
                LocationType = loc.LocationType,
                Provider = loc.Provider,
                ProviderStatus = loc.ProviderStatus,
                HorizontalAccuracy = loc.HorizontalAccuracy,
                VerticalAccuracy = loc.VerticalAccuracy,
                Course = loc.Course,
                Speed = loc.Speed,
                TimestampSnapshot = loc.TimeSnapshot

            }));

            foreach (var request in requestEnvelope.Requests)
            {
                sig.RequestHash.Add(
                    Utils.GenerateRequestHash(authSeed, request.ToByteArray(), _deviceInfo.VersionData.HashSeed1)
                    );
            }


            Signature.Types.SensorInfo sensorInfo = new Signature.Types.SensorInfo()
            {
                MagneticFieldX = normAccel.X,
                MagneticFieldY = normAccel.Y,
                MagneticFieldZ = normAccel.Z,
                RotationRateX = -_deviceInfo.Sensors.AccelRawX,
                RotationRateY = -_deviceInfo.Sensors.AccelRawY,
                RotationRateZ = -_deviceInfo.Sensors.AccelRawZ,
                LinearAccelerationX = _deviceInfo.Sensors.MagnetometerX,
                LinearAccelerationY = _deviceInfo.Sensors.MagnetometerY,
                LinearAccelerationZ = _deviceInfo.Sensors.MagnetometerZ,
                AttitudePitch = _deviceInfo.Sensors.GyroscopeRawX,
                AttitudeRoll = _deviceInfo.Sensors.GyroscopeRawY,
                AttitudeYaw = _deviceInfo.Sensors.GyroscopeRawZ,
                GravityX = _deviceInfo.Sensors.AngleNormalizedX,
                GravityY = _deviceInfo.Sensors.AngleNormalizedY,
                GravityZ = _deviceInfo.Sensors.AngleNormalizedZ,
                Status = _deviceInfo.Sensors.AccelerometerAxes,
                MagneticFieldAccuracy = 10,
                TimestampSnapshot = (ulong)(_deviceInfo.Sensors.TimeSnapshot - _random.Next(150, 260))
            };

            sig.SensorInfo.Add(sensorInfo);

            requestEnvelope.PlatformRequests.Add(new PlatformRequest()
            {
                Type = POGOProtos.Networking.Platform.PlatformRequestType.SendEncryptedSignature,
                RequestMessage = ByteString.CopyFrom(PCrypt.encrypt(sig.ToByteArray(), (uint)_deviceInfo.TimeSnapshot))
            });

            return requestEnvelope;
        }

        public RequestEnvelope GetRequestEnvelope(params Request[] customRequests)
        {
            return SetRequestEnvelopeUnknown6(new RequestEnvelope
            {
                StatusCode = 2, //1

                RequestId = 1469378659230941192, //3
                Requests = {customRequests}, //4

                //Unknown6 = , //6
                Latitude = _latitude, //7
                Longitude = _longitude, //8
                Accuracy = _accuracy, //9
                AuthTicket = _authTicket, //11
                MsSinceLastLocationfix = _random.Next(500, 1000) //12
        });
        }

        public RequestEnvelope GetInitialRequestEnvelope(params Request[] customRequests)
        {
            return SetRequestEnvelopeUnknown6(new RequestEnvelope
            {
                StatusCode = 2, //1

                RequestId = 1469378659230941192, //3
                Requests = { customRequests }, //4

                //Unknown6 = , //6
                Latitude = _latitude, //7
                Longitude = _longitude, //8
                Accuracy = _accuracy, //9
                AuthInfo = new AuthInfo
                {
                    Provider = _authType == AuthType.Google ? "google" : "ptc",
                    Token = new AuthInfo.Types.JWT
                    {
                        Contents = _authToken,
                        Unknown2 = 14
                    }
                }, //10
                MsSinceLastLocationfix = _random.Next(500, 1000) //12
            });
        }

        public RequestEnvelope GetRequestEnvelope(RequestType type, IMessage message)
        {
            return GetRequestEnvelope(new Request
            {
                RequestType = type,
                RequestMessage = message.ToByteString()
            });
        }
    }
}
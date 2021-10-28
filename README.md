# Azure IoT Edge Module implementing the Open Charge Point Protocol (OCPP) V1.5 and V1.6 Central System.

The Central System is a server responsible for communicating with charging stations and provides user authentication, billing and charge point reservation services. There is a companion dashboard project for this located [here](https://github.com/barnstee/EVChargingDashboard).

## Deployment

A deployment manifest for Azure IoT Edge can be found in the /schemas/iotedge folder. The cloud connectivity configuration is then automatically received from the Azure IoT Edge runtime.

## Configuration Settings

The environment variable "RUN_TESTS" can be set to "1" to run a simulation of 2 charging stations sending messages to the Central Station in an alternating fashion which is useful for testing purposes.


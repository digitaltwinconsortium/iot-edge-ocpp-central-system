# Docker container implementing the Open Charge Point Protocol (OCPP) V1.5 and V1.6 Central System.

The Central System is a server responsible for communicating with charging stations and provides user authentication, billing and charge point reservation services. There is a companion dashboard project for this located [here](https://github.com/barnstee/EVChargingDashboard).

## Deployment
`
docker run ghcr.io/digitaltwinconsortium/iot-edge-ocpp-central-system:main`

TODO!

## Configuration Settings
The MQTT broker receiving telemetry messages from this container is configured via environment variables:

* CreateMQTTSASToken - set to "1" to create a SAS token for authentication
* UseTLS - set to "1" to use secure MQTT (MQTTS, leveraging TLS)
* MQTTBrokerName
* MQTTClientName
* MQTTUsername
* MQTTPassword
* MQTTMessageTopic
* Publishing_Interval - the interval in which OCPP telemetry messages should be sent to the MQTT broker

Additionally, the environment variable "RUN_TESTS" can be set to "1" to run a simulation of 2 charging stations sending messages to the Central Station in an alternating fashion which is useful for testing purposes.

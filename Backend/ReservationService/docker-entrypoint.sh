#!/bin/bash
set -e

echo "Waiting for RabbitMQ to be ready..."
sleep 15

echo "Starting Reservation Service..."
exec dotnet ReservationService.dll

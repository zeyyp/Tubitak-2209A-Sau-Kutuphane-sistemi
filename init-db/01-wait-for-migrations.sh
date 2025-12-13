#!/bin/bash
set -e

echo "Waiting for services to complete migrations..."
sleep 30

echo "Checking if identity tables exist..."
until psql -U postgres -d LibraryReservation -c "SELECT 1 FROM \"AspNetRoles\" LIMIT 1;" > /dev/null 2>&1
do
  echo "Waiting for identity tables to be created..."
  sleep 5
done

echo "Checking if reservation tables exist..."
until psql -U postgres -d LibraryReservation -c "SELECT 1 FROM \"Tables\" LIMIT 1;" > /dev/null 2>&1
do
  echo "Waiting for reservation tables to be created..."
  sleep 5
done

echo "All migrations completed!"

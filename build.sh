#!/usr/bin/env bash

# Exit immediately if a command exits with a non-zero status.
set -e

# This script is a simple wrapper around the C# build script.
# It passes all command-line arguments directly to the script.
# The '--' separates arguments for 'dotnet run' from arguments for the application.
dotnet run .build/targets.cs -- "$@"
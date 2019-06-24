#!/bin/bash
if test "$OS" = "Windows_NT"
then
  # use .Net
  .paket/paket.exe restore
  exit_code=$?
  if [ $exit_code -ne 0 ]; then
  	exit $exit_code
  fi

else
  # use mono
  mono .paket/paket.exe restore
  exit_code=$?
  if [ $exit_code -ne 0 ]; then
  	exit $exit_code
  fi
  
fi

dotnet run --project buildProj\Build.fsproj %*
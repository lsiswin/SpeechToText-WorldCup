@echo off
title SpeechToText C# API Backend
echo Starting C# Web API Backend...
echo (Note: On first run, this will automatically download FFmpeg.exe and the Whisper model. Please wait.)
echo.
cd backend\SpeechToText.Api
dotnet run
pause

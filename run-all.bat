@echo off
title SpeechToText System Runner
echo ======================================================
echo           Speech-to-Text System Runner
echo ======================================================
echo.
echo Launching frontend and backend in separate windows...
echo.

start run-backend.bat
start run-frontend.bat

echo ------------------------------------------------------
echo Services started:
echo [Frontend] will run at http://localhost:5173
echo [Backend]  will run at http://localhost:5000 (Swagger: http://localhost:5000/swagger)
echo.
echo Note: The backend will auto-download the Whisper model (140MB)
echo and FFmpeg on the first run. Please keep internet active.
echo ------------------------------------------------------
echo.
pause

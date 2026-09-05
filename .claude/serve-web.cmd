@echo off
rem Wrapper so the preview launcher does not have to resolve an executable under
rem "C:\Program Files" -- it spawns runtimeExecutable without quoting the path, so any
rem exe under a directory with a space in it fails as 'C:\Program' is not recognized.
rem This file lives at a space-free path; cmd then resolves npm from PATH itself.
cd /d "%~dp0.."
npm --prefix posflow-web start

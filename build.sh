#!/bin/sh
# Builds PublicIpTray.exe with Mono's C# compiler, embedding the flag PNGs.
set -e
cd "$(dirname "$0")"
res=""
for f in assets/flags/*.png; do
  res="$res -resource:$f,flag.$(basename "$f" .png)"
done
mcs -target:winexe -platform:anycpu -optimize+ \
  -r:System.Windows.Forms.dll -r:System.Drawing.dll \
  $res -out:PublicIpTray.exe src/PublicIpTray.cs

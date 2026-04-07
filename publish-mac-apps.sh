#!/usr/bin/env bash
set -euo pipefail

APP_NAME="RookieMacOS"
EXEC_NAME="RookieMacOS.UI"

publish_one() {
  RID="$1"
  RAW_DIR="publish/raw/$RID"
  APP_DIR="publish/$RID/${APP_NAME}.app"
  MACOS_DIR="$APP_DIR/Contents/MacOS"
  RES_DIR="$APP_DIR/Contents/Resources"

  echo "Publishing $RID..."
  rm -rf "$RAW_DIR" "$APP_DIR"

  dotnet publish RookieMacOS.UI/RookieMacOS.UI.csproj \
    -c Release \
    -r "$RID" \
    --self-contained false \
    /p:UseAppHost=true \
    -o "$RAW_DIR"

  mkdir -p "$MACOS_DIR" "$RES_DIR"
  cp -R "$RAW_DIR"/. "$MACOS_DIR"/

  cat > "$APP_DIR/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>${APP_NAME}</string>
  <key>CFBundleDisplayName</key>
  <string>${APP_NAME}</string>
  <key>CFBundleIdentifier</key>
  <string>com.rookie.macos</string>
  <key>CFBundleVersion</key>
  <string>1.0.0</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0.0</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleExecutable</key>
  <string>${EXEC_NAME}</string>
  <key>LSMinimumSystemVersion</key>
  <string>11.0</string>
</dict>
</plist>
EOF

  chmod +x "$MACOS_DIR/$EXEC_NAME" || true
  echo "Created $APP_DIR"
}

publish_one osx-x64
publish_one osx-arm64

echo
echo "Done."
echo "Intel app: publish/osx-x64/${APP_NAME}.app"
echo "Apple Silicon app: publish/osx-arm64/${APP_NAME}.app"

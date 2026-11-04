# Google Play Console Store Listing & Release Guide for **Tappy**

---

## 1. Copy-Paste Store Listing Fields

### App Name (Max 30 characters)
```text
Tappy: Baby Sensory Touch Game
```

### Short Description (Max 80 characters)
```text
Baby-proof high-contrast sensory shapes, bubbles, stars & chimes. 100% ad-free!
```

### Full Description (Max 4000 characters)
```text
Tappy is a calm, high-contrast sensory touch game built specifically for babies, infants, and toddlers—and engineered to be completely baby-proof for parents.

Designed on a pure black (#000000) canvas for maximum infant visual contrast, Tappy rewards every poke, pat, and multi-finger smush with cheerful smiling shapes, gentle marimba & xylophone chimes, and crisp tactile haptics.

★ FOUR SENSORY PLAY MODES FOR BABIES ★
• Shape Buddies: Large, high-contrast smiling geometric characters (Circle, Star, Square, Triangle, Heart, Hexagon) that pop with stars and jump to a new spot on touch.
• Bubble Drift: Iridescent floating soap bubbles carrying cute shape buddies that babies can pop with one or multiple fingers at once.
• Starlight Tap: Designed for younger infants—tapping anywhere on the screen erupts into colorful starlight fireworks and harp chimes.
• Rainbow Chimes: Six giant, full-screen musical xylophone keys (DO, RE, MI, SOL, LA, DO+) with bouncing faces and warm bell tones.

★ BUILT FOR CHAOTIC INFANT MULTI-TOUCH ★
• Palm-Rest Immune: Even if your baby rests a palm or hand on the screen, pokes with their other fingers still trigger immediate pops and sounds—the game never locks up.
• Drag Ignored: Ignores accidental finger dragging and edge swipes.

★ PARENT-FRIENDLY SCREEN PINNING & HUB ★
• Android Screen Pinning: Automatically pins Tappy to the screen so babies cannot accidentally exit to your home screen, open other apps, or place calls.
• Effortless Parent Unlock: Hold the top-right Parent Pill for 1.5 seconds and tap 1 → 2 → 3 to immediately unpin the screen, switch play modes, mute audio, toggle vibration, or close the app.

★ 100% SAFE, OFFLINE & AD-FREE ★
• Zero ads, zero in-app purchases, and zero external links.
• Zero data collection—Tappy does not even request Internet permission and runs 100% offline.
```

---

## 2. Generated Graphics Ready to Upload (`PlayStoreAssets/`)

| Play Console Slot | File Path | Dimensions |
| :--- | :--- | :--- |
| **App Icon** | `PlayStoreAssets/icon-512x512.png` | `512 × 512 px` |
| **Feature Graphic** | `PlayStoreAssets/feature-graphic-1024x500.png` | `1024 × 500 px` |
| **Phone Screenshots (16:9)** | `PlayStoreAssets/Screenshots/Phone/01-shape-buddies-1920x1080.png`<br>`PlayStoreAssets/Screenshots/Phone/02-bubble-drift-1920x1080.png`<br>`PlayStoreAssets/Screenshots/Phone/03-starlight-tap-1920x1080.png`<br>`PlayStoreAssets/Screenshots/Phone/04-rainbow-chimes-1920x1080.png`<br>`PlayStoreAssets/Screenshots/Phone/05-parent-hub-1920x1080.png` | `1920 × 1080 px` |
| **7-inch Tablet Screenshots** | `PlayStoreAssets/Screenshots/Tablet-7in/01-shape-buddies-1920x1200.png`<br>`PlayStoreAssets/Screenshots/Tablet-7in/02-rainbow-chimes-1920x1200.png` | `1920 × 1200 px` |
| **10-inch Tablet Screenshots** | `PlayStoreAssets/Screenshots/Tablet-10in/01-shape-buddies-2560x1600.png`<br>`PlayStoreAssets/Screenshots/Tablet-10in/02-bubble-drift-2560x1600.png` | `2560 × 1600 px` |

---

## 3. How to Build the Signed Release `.aab` Bundle

### Step 1: Generate Your Upload Keystore (Once)
Run this in PowerShell inside `c:\Users\abtcr\Desktop\zoi`:
```powershell
& "C:\Program Files\Android\openjdk\jdk-21.0.8\bin\keytool.exe" -genkeypair -v -keystore tappy-upload.keystore -alias tappy -keyalg RSA -keysize 2048 -validity 10000
```

### Step 2: Publish the Signed `.aab` for Google Play Console
```powershell
dotnet publish -f net9.0-android -c Release -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=tappy-upload.keystore -p:AndroidSigningKeyAlias=tappy -p:AndroidSigningKeyPass=YOUR_PASSWORD -p:AndroidSigningStorePass=YOUR_PASSWORD
```
Your signed `.aab` file will be generated at:
`bin\Release\net9.0-android\android-arm64\publish\com.tappy.sensorygame-Signed.aab`


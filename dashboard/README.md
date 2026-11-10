# win-collect Dashboard

A web-based monitoring dashboard that reads device and session data from Firebase in real-time.

## Setup

```bash
cd dashboard
npm install
npm run dev
```

Then open: http://localhost:5173

## Build for production

```bash
npm run build
# Output in dashboard/dist/ — deploy to Firebase Hosting, Vercel, Netlify, etc.
```

## What it shows

- All registered computers and their monitoring status
- Real-time live updates (Firebase onSnapshot)
- Session history per device with event counts
- Last-seen timestamps

## Firebase requirements

This dashboard uses the same Firebase project as the desktop app.
Ensure Firestore Security Rules allow authenticated or public reads from the dashboard.

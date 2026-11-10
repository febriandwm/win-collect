# Firebase Setup Guide

## Overview

win-collect uses Firebase for cloud synchronization.
The project is pre-configured with the win-collect Firebase project.

## Firebase Configuration

The application is pre-configured with these settings:

| Setting | Value |
|---|---|
| Project ID | win-collect |
| Auth Domain | win-collect.firebaseapp.com |

## Firestore Security Rules

Deploy these rules to your Firebase project to restrict write access:

```
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    // Each device can only write its own records
    match /devices/{deviceId} {
      allow read: if request.auth != null;
      allow write: if request.auth != null;

      match /sessions/{sessionId} {
        allow read: if request.auth != null;
        allow write: if request.auth != null;

        match /events/{eventId} {
          allow read: if request.auth != null;
          allow write: if request.auth != null;
        }
      }
    }
  }
}
```

## Firebase Authentication

The app uses Anonymous Authentication.
Enable it in the Firebase Console:

1. Go to Firebase Console → Authentication
2. Sign-in method → Anonymous → Enable

## Data Structure

```
/devices/{deviceId}
  - computerName: string
  - operatingSystem: string
  - applicationVersion: string
  - registeredAt: timestamp
  - lastSeenAt: timestamp
  - monitoringStatus: string

/devices/{deviceId}/sessions/{sessionId}
  - startedAt: timestamp
  - endedAt: timestamp (nullable)
  - status: string
  - eventCount: integer

/devices/{deviceId}/sessions/{sessionId}/events/{eventId}
  - sequence: integer
  - timestamp: timestamp
  - eventType: string (KEY_DOWN | KEY_UP)
  - key: string
```

// firebase.js — Initialize Firebase (from your Firebase console config)
import { initializeApp } from "firebase/app";
import { getFirestore } from "firebase/firestore";
import { getAnalytics } from "firebase/analytics";

// Your web app's Firebase configuration
const firebaseConfig = {
  apiKey: "AIzaSyDMC8KPtjkKcQXOElAnX01UeYuwEaD_ooE",
  authDomain: "win-collect.firebaseapp.com",
  projectId: "win-collect",
  storageBucket: "win-collect.firebasestorage.app",
  messagingSenderId: "810749046646",
  appId: "1:810749046646:web:527666cd24d115548bd457",
  measurementId: "G-W1CMTYKFZT"
};

// Initialize Firebase
const app = initializeApp(firebaseConfig);

// Firestore database — used to read device/session data
export const db = getFirestore(app);

// Analytics (optional)
export const analytics = getAnalytics(app);

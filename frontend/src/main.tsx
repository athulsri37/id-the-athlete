import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router";
import App from "./App";
import ControlRoom from "./admin/ControlRoom";
import "./styles/index.css";
import "./styles/themes.css";

// The public game is routed (see App.tsx and routes/paths). /control-room
// is deliberately not one of its routes: a raw pathname check here, so it's
// reachable only by navigating to the URL directly and never appears in any
// public link or nav.
const isControlRoom = window.location.pathname === "/control-room";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    {isControlRoom ? (
      <ControlRoom />
    ) : (
      <BrowserRouter>
        <App />
      </BrowserRouter>
    )}
  </React.StrictMode>
);

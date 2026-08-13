import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import ControlRoom from "./admin/ControlRoom";
import "./styles/index.css";
import "./styles/themes.css";

// No router library: the public game is a single-page state machine (see
// App.tsx's Screen union) with no URL awareness at all. /control-room is
// the one exception -- a raw pathname check here, deliberately not a
// route registered anywhere else, so it's reachable only by navigating to
// the URL directly and never appears in any public link or nav.
const isControlRoom = window.location.pathname === "/control-room";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    {isControlRoom ? <ControlRoom /> : <App />}
  </React.StrictMode>
);

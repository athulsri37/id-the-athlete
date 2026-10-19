import { createContext, useContext } from "react";

// The active theme's name (e.g. "retro"), loaded once when the app starts
// and also set as <html data-theme>, which the CSS variables key off. Screens
// that draw theme-specific artwork (the scenery) read it from here. Empty
// outside the app, e.g. in a component test, which means "no artwork".
export const ThemeContext = createContext("");

export function useTheme(): string {
  return useContext(ThemeContext);
}

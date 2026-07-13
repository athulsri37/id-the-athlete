/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        court: {
          green: "#2E5339",
          clay: "#B5602B",
          chalk: "#F7F4EC",
        },
        ace: {
          500: "#C9E265",
          600: "#AECB4C",
        },
      },
      fontFamily: {
        display: ["'Bebas Neue'", "sans-serif"],
      },
    },
  },
  plugins: [],
};

/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './index.html',
    './src/**/*.{js,ts,jsx,tsx}',
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['system-ui','-apple-system','Inter','Roboto','Segoe UI','Arial','sans-serif'],
      },
      colors: {
        brand: {
          50: '#f0f7ff',
          100: '#d9ecff',
          500: '#1d72ff',
          600: '#155dcc',
        }
      }
    },
  },
  plugins: [],
};
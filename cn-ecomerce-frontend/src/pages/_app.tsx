import "../styles/globals.css";
import { CartProvider } from "../context/CartContext";
import Header from "../components/Header";
import type { AppProps } from "next/app";

function MyApp({ Component, pageProps }: AppProps) {
  return (
    <CartProvider>
      <Header />
      <Component {...pageProps} />
    </CartProvider>
  );
}

export default MyApp;

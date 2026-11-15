import type { Metadata } from "next";
import { Figtree, Syne } from "next/font/google";
import "./globals.css";

const body = Figtree({
  subsets: ["latin"],
  variable: "--font-body",
});

const display = Syne({
  subsets: ["latin"],
  variable: "--font-display",
  weight: ["500", "600", "700", "800"],
});

export const metadata: Metadata = {
  title: "TechBench NQ",
  description:
    "Self-hosted repair management for TechBench NQ — phones, tablets and computers",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en-AU">
      <body className={`${body.variable} ${display.variable}`}>{children}</body>
    </html>
  );
}

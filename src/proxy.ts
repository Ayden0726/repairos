import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";

const PUBLIC = [
  "/login",
  "/setup",
  "/manifest.webmanifest",
  "/sw.js",
  "/icons",
  "/api/health",
  "/api/setup",
];

export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  if (
    pathname.startsWith("/_next") ||
    pathname.startsWith("/favicon") ||
    pathname.includes(".") && !pathname.startsWith("/api")
  ) {
    return NextResponse.next();
  }

  const isPublic = PUBLIC.some((p) => pathname === p || pathname.startsWith(`${p}/`));
  const session = request.cookies.get("workshopos_session")?.value;
  const setup = request.cookies.get("workshopos_setup")?.value;

  if (!isPublic && !session && pathname !== "/setup") {
    const url = request.nextUrl.clone();
    url.pathname = "/login";
    url.searchParams.set("next", pathname);
    return NextResponse.redirect(url);
  }

  if (session && (pathname === "/login")) {
    const url = request.nextUrl.clone();
    url.pathname = "/";
    return NextResponse.redirect(url);
  }

  const response = NextResponse.next();
  response.headers.set("X-Frame-Options", "DENY");
  response.headers.set("X-Content-Type-Options", "nosniff");
  response.headers.set("Referrer-Policy", "strict-origin-when-cross-origin");
  response.headers.set("Permissions-Policy", "camera=(self), microphone=(), geolocation=()");
  if (setup) response.headers.set("x-setup", "1");
  return response;
}

export const config = {
  matcher: ["/((?!_next/static|_next/image).*)"],
};

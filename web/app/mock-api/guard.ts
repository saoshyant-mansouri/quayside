export function mockDisabled(): Response | null {
  if (process.env.NEXT_PUBLIC_USE_MOCK === "1") return null;
  return new Response(null, { status: 404 });
}

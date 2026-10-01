import { ImageResponse } from "next/og";
import { AUTHOR_NAME, SITE_NAME } from "@/lib/site";

export const SOCIAL_IMAGE_SIZE = { width: 1200, height: 630 };

export function socialImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between",
          background: "#141412",
          color: "#f1eee6",
          padding: 72,
        }}
      >
        <div style={{ display: "flex", width: 96, height: 12, background: "#b3a01c" }} />
        <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
          <div style={{ display: "flex", fontSize: 148, fontWeight: 700, letterSpacing: -4 }}>
            {SITE_NAME}
          </div>
          <div style={{ display: "flex", fontSize: 40, color: "#c3c9b0" }}>
            Retrieval-augmented generation on Azure, built with C# and .NET
          </div>
        </div>
        <div style={{ display: "flex", fontSize: 30, color: "#dcc5b9" }}>
          Built by {AUTHOR_NAME} · mhdmansouri.com
        </div>
      </div>
    ),
    SOCIAL_IMAGE_SIZE,
  );
}

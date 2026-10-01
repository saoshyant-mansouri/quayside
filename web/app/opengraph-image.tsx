import { SOCIAL_IMAGE_SIZE, socialImage } from "@/lib/social-image";

export const alt = "MSC RAG: retrieval-augmented generation on Azure, built with C# and .NET";
export const size = SOCIAL_IMAGE_SIZE;
export const contentType = "image/png";

export default function Image() {
  return socialImage();
}

import { notFound } from "next/navigation"
import { getChatShare } from "@/lib/chat-api"
import { ShareConversation } from "./share-conversation"

export const dynamic = "force-dynamic"

interface SharePageProps {
  params: Promise<{ shareId: string }>
}

export default async function SharePage({ params }: SharePageProps) {
  const { shareId } = await params
  if (!shareId) {
    notFound()
  }

  const share = await getChatShare(shareId).catch((error) => {
    console.error("Failed to load share", error)
    notFound()
  })

  return <ShareConversation share={share} />
}

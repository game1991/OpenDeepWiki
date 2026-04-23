import { notFound, redirect } from "next/navigation";

const idPattern = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$/;

interface OwnerPageProps {
  params: Promise<{
    owner: string;
  }>;
}

export default async function OwnerPage({ params }: OwnerPageProps) {
  const { owner } = await params;
  if (!idPattern.test(owner)) {
    notFound();
  }

  redirect(`/admin/repositories/${owner}`);
}


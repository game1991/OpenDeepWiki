import { fetchMindMap } from "@/lib/repository-api";
import { MindMapPageContent } from "@/components/repo/mindmap-page-content";
import { decodeRouteSegment } from "@/lib/repo-route";

interface MindMapPageProps {
  params: Promise<{
    owner: string;
    repo: string;
  }>;
  searchParams: Promise<{
    branch?: string;
    lang?: string;
  }>;
}

async function getMindMapData(owner: string, repo: string, branch?: string, lang?: string) {
  try {
    return await fetchMindMap(owner, repo, branch, lang);
  } catch {
    return null;
  }
}

export default async function MindMapPage({ params, searchParams }: MindMapPageProps) {
  const { owner, repo } = await params;
  const decodedOwner = decodeRouteSegment(owner);
  const decodedRepo = decodeRouteSegment(repo);
  const resolvedSearchParams = await searchParams;
  const branch = resolvedSearchParams?.branch;
  const lang = resolvedSearchParams?.lang;

  const mindMap = await getMindMapData(decodedOwner, decodedRepo, branch, lang);

  return (
    <MindMapPageContent
      owner={decodedOwner}
      repo={decodedRepo}
      mindMap={mindMap}
    />
  );
}

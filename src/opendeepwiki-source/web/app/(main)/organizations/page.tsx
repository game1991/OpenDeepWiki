"use client";

import React, { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/contexts/auth-context";
import { AppLayout } from "@/components/app-layout";
import {
  getMyDepartments,
  getMyDepartmentRepositories,
  UserDepartment,
  DepartmentRepository,
} from "@/lib/organization-api";
import {
  Loader2,
  Building2,
  GitBranch,
  RefreshCw,
  ExternalLink,
  Clock,
  CheckCircle,
  XCircle,
  AlertCircle,
} from "lucide-react";
import { toast } from "sonner";
import { buildRepoBasePath } from "@/lib/repo-route";

const statusConfig: Record<string, { icon: React.ElementType; color: string; label: string }> = {
  Pending: { icon: Clock, color: "text-yellow-500", label: "等待处理" },
  Processing: { icon: Loader2, color: "text-blue-500", label: "处理中" },
  Completed: { icon: CheckCircle, color: "text-green-500", label: "已完成" },
  Failed: { icon: XCircle, color: "text-red-500", label: "失败" },
  Unknown: { icon: AlertCircle, color: "text-gray-500", label: "未知" },
};


export default function OrganizationsPage() {
  const { user, isLoading: authLoading } = useAuth();
  const [departments, setDepartments] = useState<UserDepartment[]>([]);
  const [repositories, setRepositories] = useState<DepartmentRepository[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchData = useCallback(async () => {
    if (!user) return;
    
    setLoading(true);
    try {
      const [depts, repos] = await Promise.all([
        getMyDepartments(),
        getMyDepartmentRepositories(),
      ]);
      setDepartments(depts);
      setRepositories(repos);
    } catch (error) {
      console.error("Failed to fetch organization data:", error);
      toast.error("获取组织数据失败");
    } finally {
      setLoading(false);
    }
  }, [user]);

  useEffect(() => {
    if (user) {
      fetchData();
    } else if (!authLoading) {
      setLoading(false);
    }
  }, [user, authLoading, fetchData]);

  const content = () => {
    if (authLoading || loading) {
      return (
        <div className="flex h-[50vh] items-center justify-center">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      );
    }

    if (!user) {
      return (
        <Card className="flex h-64 flex-col items-center justify-center">
          <Building2 className="h-12 w-12 text-muted-foreground/50" />
          <p className="mt-4 text-muted-foreground">请先登录查看您的组织</p>
        </Card>
      );
    }

    if (departments.length === 0) {
      return (
        <Card className="flex h-64 flex-col items-center justify-center">
          <Building2 className="h-12 w-12 text-muted-foreground/50" />
          <p className="mt-4 text-muted-foreground">您还没有加入任何部门</p>
          <p className="mt-2 text-sm text-muted-foreground">请联系管理员将您添加到部门中</p>
        </Card>
      );
    }

    return (
      <div className="space-y-6">
        {/* 部门列表 */}
        <div>
          <h2 className="text-lg font-semibold mb-4">我的部门</h2>
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {departments.map((dept) => (
              <Card key={dept.id} className="p-4">
                <div className="flex items-center gap-3">
                  <div className="rounded-full bg-primary/10 p-2">
                    <Building2 className="h-5 w-5 text-primary" />
                  </div>
                  <div>
                    <h3 className="font-medium">{dept.name}</h3>
                    {dept.isManager && (
                      <span className="text-xs text-primary">部门主管</span>
                    )}
                  </div>
                </div>
                {dept.description && (
                  <p className="mt-2 text-sm text-muted-foreground line-clamp-2">
                    {dept.description}
                  </p>
                )}
              </Card>
            ))}
          </div>
        </div>


        {/* 仓库列表 */}
        <div>
          <h2 className="text-lg font-semibold mb-4">部门仓库</h2>
          {repositories.length === 0 ? (
            <Card className="flex h-32 flex-col items-center justify-center">
              <GitBranch className="h-8 w-8 text-muted-foreground/50" />
              <p className="mt-2 text-sm text-muted-foreground">暂无分配的仓库</p>
            </Card>
          ) : (
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
              {repositories.map((repo) => {
                const status = statusConfig[repo.statusName] || statusConfig.Unknown;
                const StatusIcon = status.icon;
                
                return (
                  <Card key={repo.repositoryId} className="p-4">
                    <div className="flex items-start justify-between">
                      <div className="flex items-center gap-3">
                        <div className="rounded-full bg-primary/10 p-2">
                          <GitBranch className="h-5 w-5 text-primary" />
                        </div>
                        <div>
                          <h3 className="font-medium">{repo.orgName}/{repo.repoName}</h3>
                          <span className="text-xs text-muted-foreground">
                            {repo.departmentName}
                          </span>
                        </div>
                      </div>
                      <div className={`flex items-center gap-1 ${status.color}`}>
                        <StatusIcon className={`h-4 w-4 ${repo.statusName === "Processing" ? "animate-spin" : ""}`} />
                        <span className="text-xs">{status.label}</span>
                      </div>
                    </div>
                    <div className="mt-4 flex gap-2">
                      {repo.statusName === "Completed" && (
                        <Link href={buildRepoBasePath(repo.orgName, repo.repoName)}>
                          <Button size="sm" variant="outline">
                            <ExternalLink className="mr-1 h-3 w-3" />
                            查看文档
                          </Button>
                        </Link>
                      )}
                    </div>
                  </Card>
                );
              })}
            </div>
          )}
        </div>
      </div>
    );
  };

  return (
    <AppLayout activeItem="我的组织">
      <div className="container mx-auto p-6">
        <div className="flex items-center justify-between mb-6">
          <h1 className="text-2xl font-bold">我的组织</h1>
          {user && (
            <Button variant="outline" onClick={fetchData}>
              <RefreshCw className="mr-2 h-4 w-4" />
              刷新
            </Button>
          )}
        </div>
        {content()}
      </div>
    </AppLayout>
  );
}

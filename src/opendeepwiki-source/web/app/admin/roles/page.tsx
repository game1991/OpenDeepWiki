"use client";

import React, { useEffect, useState, useCallback } from "react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  getRoles,
  createRole,
  updateRole,
  deleteRole,
  AdminRole,
} from "@/lib/admin-api";
import {
  Loader2,
  Trash2,
  Edit,
  RefreshCw,
  Plus,
  Shield,
  Users,
} from "lucide-react";
import { toast } from "sonner";
import { useTranslations } from "@/hooks/use-translations";
import { useLocale } from "next-intl";

export default function AdminRolesPage() {
  const [roles, setRoles] = useState<AdminRole[]>([]);
  const [loading, setLoading] = useState(true);
  const [showDialog, setShowDialog] = useState(false);
  const [editingRole, setEditingRole] = useState<AdminRole | null>(null);
  const [deleteId, setDeleteId] = useState<string | null>(null);
  const [formData, setFormData] = useState({ name: "", description: "" });
  const t = useTranslations();
  const locale = useLocale();

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const result = await getRoles();
      setRoles(result);
    } catch (error) {
      console.error("Failed to fetch roles:", error);
      toast.error(t('admin.toast.fetchRoleFailed'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const openCreateDialog = () => {
    setEditingRole(null);
    setFormData({ name: "", description: "" });
    setShowDialog(true);
  };

  const openEditDialog = (role: AdminRole) => {
    setEditingRole(role);
    setFormData({ name: role.name, description: role.description || "" });
    setShowDialog(true);
  };

  const handleSave = async () => {
    if (!formData.name.trim()) {
      toast.error(t('admin.toast.enterRoleName'));
      return;
    }
    try {
      if (editingRole) {
        await updateRole(editingRole.id, formData);
        toast.success(t('admin.toast.updateSuccess'));
      } else {
        await createRole(formData);
        toast.success(t('admin.toast.createSuccess'));
      }
      setShowDialog(false);
      fetchData();
    } catch (error) {
      toast.error(editingRole ? t('admin.toast.updateFailed') : t('admin.toast.createFailed'));
    }
  };

  const handleDelete = async () => {
    if (!deleteId) return;
    try {
      await deleteRole(deleteId);
      toast.success(t('admin.toast.deleteSuccess'));
      setDeleteId(null);
      fetchData();
    } catch (error: any) {
      toast.error(error.message || t('admin.toast.deleteFailed'));
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">{t('admin.roles.title')}</h1>
        <div className="flex gap-2">
          <Button variant="outline" onClick={fetchData}>
            <RefreshCw className="mr-2 h-4 w-4" />
            {t('admin.common.refresh')}
          </Button>
          <Button onClick={openCreateDialog}>
            <Plus className="mr-2 h-4 w-4" />
            {t('admin.roles.createRole')}
          </Button>
        </div>
      </div>

      {/* 角色列表 */}
      {loading ? (
        <div className="flex h-64 items-center justify-center">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {roles.map((role) => (
            <Card key={role.id} className="p-6">
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-3">
                  <div className="rounded-full bg-primary/10 p-2">
                    <Shield className="h-5 w-5 text-primary" />
                  </div>
                  <div>
                    <h3 className="font-semibold">{role.name}</h3>
                    {role.isSystem && (
                      <span className="text-xs text-muted-foreground">{t('admin.roles.systemRole')}</span>
                    )}
                  </div>
                </div>
                {!role.isSystem && (
                  <div className="flex gap-1">
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => openEditDialog(role)}
                    >
                      <Edit className="h-4 w-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => setDeleteId(role.id)}
                    >
                      <Trash2 className="h-4 w-4 text-red-500" />
                    </Button>
                  </div>
                )}
              </div>
              {role.description && (
                <p className="mt-3 text-sm text-muted-foreground">
                  {role.description}
                </p>
              )}
              <div className="mt-4 flex items-center gap-2 text-sm text-muted-foreground">
                <Users className="h-4 w-4" />
                <span>{t('admin.roles.usersCount', { count: role.userCount })}</span>
              </div>
              <p className="mt-2 text-xs text-muted-foreground">
                {t('admin.roles.createdAt', { date: new Date(role.createdAt).toLocaleDateString(locale === 'zh' ? 'zh-CN' : locale) })}
              </p>
            </Card>
          ))}
        </div>
      )}

      {/* 新增/编辑对话框 */}
      <Dialog open={showDialog} onOpenChange={setShowDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editingRole ? t('admin.roles.editRole') : t('admin.roles.createRole')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <label className="text-sm font-medium">{t('admin.roles.roleName')} *</label>
              <Input
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                placeholder={t('admin.roles.enterRoleName')}
                disabled={editingRole?.isSystem}
              />
            </div>
            <div>
              <label className="text-sm font-medium">{t('admin.roles.description')}</label>
              <Textarea
                value={formData.description}
                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                placeholder={t('admin.roles.enterRoleDesc')}
                rows={3}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowDialog(false)}>
              {t('admin.common.cancel')}
            </Button>
            <Button onClick={handleSave}>
              {editingRole ? t('admin.common.save') : t('admin.common.create')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* 删除确认对话框 */}
      <AlertDialog open={!!deleteId} onOpenChange={() => setDeleteId(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('admin.common.confirmDelete')}</AlertDialogTitle>
            <AlertDialogDescription>
              {t('admin.roles.deleteWarning')}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('admin.common.cancel')}</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete} className="bg-red-600 hover:bg-red-700">
              {t('admin.common.delete')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

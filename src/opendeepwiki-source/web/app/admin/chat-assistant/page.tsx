"use client";

import React, { useEffect, useState, useCallback } from "react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  getChatAssistantConfig,
  updateChatAssistantConfig,
  ChatAssistantConfigOptions,
  SelectableItem,
} from "@/lib/admin-api";
import {
  Loader2,
  RefreshCw,
  Save,
  MessageCircle,
  Bot,
  Wrench,
  Sparkles,
} from "lucide-react";
import { toast } from "sonner";
import { useTranslations } from "@/hooks/use-translations";

export default function AdminChatAssistantPage() {
  const [configOptions, setConfigOptions] = useState<ChatAssistantConfigOptions | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const t = useTranslations();

  // 编辑状态
  const [isEnabled, setIsEnabled] = useState(false);
  const [selectedModelIds, setSelectedModelIds] = useState<string[]>([]);
  const [selectedMcpIds, setSelectedMcpIds] = useState<string[]>([]);
  const [selectedSkillIds, setSelectedSkillIds] = useState<string[]>([]);
  const [defaultModelId, setDefaultModelId] = useState<string | undefined>();

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const result = await getChatAssistantConfig();
      setConfigOptions(result);
      // 初始化编辑状态
      setIsEnabled(result.config.isEnabled);
      setSelectedModelIds(result.config.enabledModelIds);
      setSelectedMcpIds(result.config.enabledMcpIds);
      setSelectedSkillIds(result.config.enabledSkillIds);
      setDefaultModelId(result.config.defaultModelId);
    } catch (error) {
      console.error("Failed to fetch chat assistant config:", error);
      toast.error(t('admin.toast.fetchConfigFailed'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleSave = async () => {
    setSaving(true);
    try {
      await updateChatAssistantConfig({
        isEnabled,
        enabledModelIds: selectedModelIds,
        enabledMcpIds: selectedMcpIds,
        enabledSkillIds: selectedSkillIds,
        defaultModelId,
      });
      toast.success(t('admin.toast.configSaveSuccess'));
      fetchData();
    } catch (error) {
      console.error("Failed to save config:", error);
      toast.error(t('admin.toast.configSaveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const toggleModel = (id: string) => {
    setSelectedModelIds((prev) =>
      prev.includes(id) ? prev.filter((i) => i !== id) : [...prev, id]
    );
  };

  const toggleMcp = (id: string) => {
    setSelectedMcpIds((prev) =>
      prev.includes(id) ? prev.filter((i) => i !== id) : [...prev, id]
    );
  };

  const toggleSkill = (id: string) => {
    setSelectedSkillIds((prev) =>
      prev.includes(id) ? prev.filter((i) => i !== id) : [...prev, id]
    );
  };

  const hasChanges = configOptions && (
    isEnabled !== configOptions.config.isEnabled ||
    JSON.stringify(selectedModelIds.sort()) !== JSON.stringify(configOptions.config.enabledModelIds.sort()) ||
    JSON.stringify(selectedMcpIds.sort()) !== JSON.stringify(configOptions.config.enabledMcpIds.sort()) ||
    JSON.stringify(selectedSkillIds.sort()) !== JSON.stringify(configOptions.config.enabledSkillIds.sort()) ||
    defaultModelId !== configOptions.config.defaultModelId
  );

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const selectedModels = configOptions?.availableModels.filter((m) =>
    selectedModelIds.includes(m.id)
  ) || [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <MessageCircle className="h-6 w-6" />
          <h1 className="text-2xl font-bold">{t('admin.chatAssistant.title')}</h1>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={fetchData}>
            <RefreshCw className="mr-2 h-4 w-4" />
            {t('admin.common.refresh')}
          </Button>
          <Button onClick={handleSave} disabled={saving || !hasChanges}>
            {saving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            {t('admin.chatAssistant.saveConfig')}
          </Button>
        </div>
      </div>

      {/* 启用开关 */}
      <Card className="p-6">
        <div className="flex items-center justify-between">
          <div className="space-y-1">
            <Label className="text-base font-medium">{t('admin.chatAssistant.enableAssistant')}</Label>
            <p className="text-sm text-muted-foreground">
              {t('admin.chatAssistant.enableDesc')}
            </p>
          </div>
          <Switch checked={isEnabled} onCheckedChange={setIsEnabled} />
        </div>
      </Card>

      {/* 模型配置 */}
      <Card className="p-6">
        <div className="space-y-4">
          <div className="flex items-center gap-2">
            <Bot className="h-5 w-5" />
            <h2 className="text-lg font-semibold">{t('admin.chatAssistant.availableModels')}</h2>
          </div>
          <p className="text-sm text-muted-foreground">
            {t('admin.chatAssistant.selectModelsDesc')}
          </p>

          {configOptions?.availableModels.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4">
              {t('admin.chatAssistant.noModels')}
            </p>
          ) : (
            <div className="grid gap-3">
              {configOptions?.availableModels.map((model) => (
                <div
                  key={model.id}
                  className="flex items-center space-x-3 rounded-lg border p-3"
                >
                  <Checkbox
                    id={`model-${model.id}`}
                    checked={selectedModelIds.includes(model.id)}
                    onCheckedChange={() => toggleModel(model.id)}
                  />
                  <div className="flex-1">
                    <Label
                      htmlFor={`model-${model.id}`}
                      className="text-sm font-medium cursor-pointer"
                    >
                      {model.name}
                    </Label>
                    {model.description && (
                      <p className="text-xs text-muted-foreground">
                        {model.description}
                      </p>
                    )}
                  </div>
                  {!model.isActive && (
                    <span className="text-xs text-yellow-600 bg-yellow-100 px-2 py-0.5 rounded">
                      {t('admin.chatAssistant.inactive')}
                    </span>
                  )}
                </div>
              ))}
            </div>
          )}

          {/* 默认模型选择 */}
          {selectedModels.length > 0 && (
            <div className="pt-4 border-t">
              <Label className="text-sm font-medium">{t('admin.chatAssistant.defaultModel')}</Label>
              <p className="text-xs text-muted-foreground mb-2">
                {t('admin.chatAssistant.defaultModelDesc')}
              </p>
              <Select value={defaultModelId} onValueChange={setDefaultModelId}>
                <SelectTrigger className="w-full max-w-xs">
                  <SelectValue placeholder={t('admin.chatAssistant.selectDefaultModel')} />
                </SelectTrigger>
                <SelectContent>
                  {selectedModels.map((model) => (
                    <SelectItem key={model.id} value={model.id}>
                      {model.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}
        </div>
      </Card>

      {/* MCP配置 */}
      <Card className="p-6">
        <div className="space-y-4">
          <div className="flex items-center gap-2">
            <Wrench className="h-5 w-5" />
            <h2 className="text-lg font-semibold">{t('admin.chatAssistant.availableMcps')}</h2>
          </div>
          <p className="text-sm text-muted-foreground">
            {t('admin.chatAssistant.selectMcpsDesc')}
          </p>

          {configOptions?.availableMcps.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4">
              {t('admin.chatAssistant.noMcps')}
            </p>
          ) : (
            <div className="grid gap-3">
              {configOptions?.availableMcps.map((mcp) => (
                <div
                  key={mcp.id}
                  className="flex items-center space-x-3 rounded-lg border p-3"
                >
                  <Checkbox
                    id={`mcp-${mcp.id}`}
                    checked={selectedMcpIds.includes(mcp.id)}
                    onCheckedChange={() => toggleMcp(mcp.id)}
                  />
                  <div className="flex-1">
                    <Label
                      htmlFor={`mcp-${mcp.id}`}
                      className="text-sm font-medium cursor-pointer"
                    >
                      {mcp.name}
                    </Label>
                    {mcp.description && (
                      <p className="text-xs text-muted-foreground">
                        {mcp.description}
                      </p>
                    )}
                  </div>
                  {!mcp.isActive && (
                    <span className="text-xs text-yellow-600 bg-yellow-100 px-2 py-0.5 rounded">
                      {t('admin.chatAssistant.inactive')}
                    </span>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </Card>

      {/* Skills配置 */}
      <Card className="p-6">
        <div className="space-y-4">
          <div className="flex items-center gap-2">
            <Sparkles className="h-5 w-5" />
            <h2 className="text-lg font-semibold">{t('admin.chatAssistant.availableSkills')}</h2>
          </div>
          <p className="text-sm text-muted-foreground">
            {t('admin.chatAssistant.selectSkillsDesc')}
          </p>

          {configOptions?.availableSkills.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4">
              {t('admin.chatAssistant.noSkills')}
            </p>
          ) : (
            <div className="grid gap-3">
              {configOptions?.availableSkills.map((skill) => (
                <div
                  key={skill.id}
                  className="flex items-center space-x-3 rounded-lg border p-3"
                >
                  <Checkbox
                    id={`skill-${skill.id}`}
                    checked={selectedSkillIds.includes(skill.id)}
                    onCheckedChange={() => toggleSkill(skill.id)}
                  />
                  <div className="flex-1">
                    <Label
                      htmlFor={`skill-${skill.id}`}
                      className="text-sm font-medium cursor-pointer"
                    >
                      {skill.name}
                    </Label>
                    {skill.description && (
                      <p className="text-xs text-muted-foreground">
                        {skill.description}
                      </p>
                    )}
                  </div>
                  {!skill.isActive && (
                    <span className="text-xs text-yellow-600 bg-yellow-100 px-2 py-0.5 rounded">
                      {t('admin.chatAssistant.inactive')}
                    </span>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </Card>

      {/* 浮动保存按钮 */}
      {hasChanges && (
        <div className="fixed bottom-6 right-6">
          <Button onClick={handleSave} disabled={saving} size="lg" className="shadow-lg">
            {saving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            {t('admin.chatAssistant.saveConfig')}
          </Button>
        </div>
      )}
    </div>
  );
}

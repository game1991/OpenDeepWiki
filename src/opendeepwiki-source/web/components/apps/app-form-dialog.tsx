"use client";

import { useState, useEffect } from "react";
import { useTranslations } from "@/hooks/use-translations";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Loader2 } from "lucide-react";
import {
  createApp,
  updateApp,
  ChatAppDto,
  CreateChatAppDto,
  UpdateChatAppDto,
  PROVIDER_TYPES,
} from "@/lib/apps-api";

interface AppFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  app?: ChatAppDto | null;
  onSuccess: () => void;
}

export function AppFormDialog({
  open,
  onOpenChange,
  app,
  onSuccess,
}: AppFormDialogProps) {
  const t = useTranslations();
  const isEditing = !!app;

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [iconUrl, setIconUrl] = useState("");
  const [enableDomainValidation, setEnableDomainValidation] = useState(false);
  const [allowedDomains, setAllowedDomains] = useState("");
  const [providerType, setProviderType] = useState("OpenAI");
  const [apiKey, setApiKey] = useState("");
  const [baseUrl, setBaseUrl] = useState("");
  const [availableModels, setAvailableModels] = useState("");
  const [defaultModel, setDefaultModel] = useState("");
  const [rateLimitPerMinute, setRateLimitPerMinute] = useState("");
  const [isActive, setIsActive] = useState(true);

  // Reset form when dialog opens/closes or app changes
  useEffect(() => {
    if (open) {
      if (app) {
        setName(app.name);
        setDescription(app.description || "");
        setIconUrl(app.iconUrl || "");
        setEnableDomainValidation(app.enableDomainValidation);
        setAllowedDomains(app.allowedDomains.join("\n"));
        setProviderType(app.providerType);
        setApiKey(""); // Don't show existing API key
        setBaseUrl(app.baseUrl || "");
        setAvailableModels(app.availableModels.join("\n"));
        setDefaultModel(app.defaultModel || "");
        setRateLimitPerMinute(app.rateLimitPerMinute?.toString() || "");
        setIsActive(app.isActive);
      } else {
        // Reset to defaults for new app
        setName("");
        setDescription("");
        setIconUrl("");
        setEnableDomainValidation(false);
        setAllowedDomains("");
        setProviderType("OpenAI");
        setApiKey("");
        setBaseUrl("");
        setAvailableModels("");
        setDefaultModel("");
        setRateLimitPerMinute("");
        setIsActive(true);
      }
      setError(null);
    }
  }, [open, app]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!name.trim()) {
      setError(t("apps.form.nameRequired"));
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      const domainsArray = allowedDomains
        .split("\n")
        .map((d) => d.trim())
        .filter((d) => d);
      const modelsArray = availableModels
        .split("\n")
        .map((m) => m.trim())
        .filter((m) => m);

      if (isEditing && app) {
        const updateDto: UpdateChatAppDto = {
          name: name.trim(),
          description: description.trim() || undefined,
          iconUrl: iconUrl.trim() || undefined,
          enableDomainValidation,
          allowedDomains: domainsArray,
          providerType,
          baseUrl: baseUrl.trim() || undefined,
          availableModels: modelsArray,
          defaultModel: defaultModel.trim() || undefined,
          rateLimitPerMinute: rateLimitPerMinute
            ? parseInt(rateLimitPerMinute)
            : undefined,
          isActive,
        };
        // Only include apiKey if it was changed
        if (apiKey.trim()) {
          updateDto.apiKey = apiKey.trim();
        }
        await updateApp(app.id, updateDto);
      } else {
        const createDto: CreateChatAppDto = {
          name: name.trim(),
          description: description.trim() || undefined,
          iconUrl: iconUrl.trim() || undefined,
          enableDomainValidation,
          allowedDomains: domainsArray,
          providerType,
          apiKey: apiKey.trim() || undefined,
          baseUrl: baseUrl.trim() || undefined,
          availableModels: modelsArray,
          defaultModel: defaultModel.trim() || undefined,
          rateLimitPerMinute: rateLimitPerMinute
            ? parseInt(rateLimitPerMinute)
            : undefined,
        };
        await createApp(createDto);
      }

      onSuccess();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : isEditing
          ? t("apps.form.updateFailed")
          : t("apps.form.createFailed")
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  const modelOptions = availableModels
    .split("\n")
    .map((m) => m.trim())
    .filter((m) => m);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {isEditing ? t("apps.form.editTitle") : t("apps.form.createTitle")}
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-6">
          {error && (
            <div className="bg-destructive/10 text-destructive px-4 py-2 rounded-md text-sm">
              {error}
            </div>
          )}

          {/* Basic Info */}
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="name">{t("apps.form.name")} *</Label>
              <Input
                id="name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder={t("apps.form.namePlaceholder")}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="description">{t("apps.form.description")}</Label>
              <Textarea
                id="description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder={t("apps.form.descriptionPlaceholder")}
                rows={2}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="iconUrl">{t("apps.form.iconUrl")}</Label>
              <Input
                id="iconUrl"
                value={iconUrl}
                onChange={(e) => setIconUrl(e.target.value)}
                placeholder={t("apps.form.iconUrlPlaceholder")}
              />
            </div>
          </div>

          {/* Domain Validation */}
          <div className="space-y-4 border-t pt-4">
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label>{t("apps.form.domainValidation")}</Label>
                <p className="text-sm text-muted-foreground">
                  {t("apps.form.domainValidationHint")}
                </p>
              </div>
              <Switch
                checked={enableDomainValidation}
                onCheckedChange={setEnableDomainValidation}
              />
            </div>

            {enableDomainValidation && (
              <div className="space-y-2">
                <Label htmlFor="allowedDomains">
                  {t("apps.form.allowedDomains")}
                </Label>
                <Textarea
                  id="allowedDomains"
                  value={allowedDomains}
                  onChange={(e) => setAllowedDomains(e.target.value)}
                  placeholder={t("apps.form.allowedDomainsPlaceholder")}
                  rows={3}
                />
              </div>
            )}
          </div>

          {/* AI Configuration */}
          <div className="space-y-4 border-t pt-4">
            <h3 className="font-medium">{t("apps.form.aiConfig")}</h3>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>{t("apps.form.providerType")}</Label>
                <Select value={providerType} onValueChange={setProviderType}>
                  <SelectTrigger className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {PROVIDER_TYPES.map((provider) => (
                      <SelectItem key={provider.value} value={provider.value}>
                        {provider.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="apiKey">{t("apps.form.apiKey")}</Label>
                <Input
                  id="apiKey"
                  type="password"
                  value={apiKey}
                  onChange={(e) => setApiKey(e.target.value)}
                  placeholder={t("apps.form.apiKeyPlaceholder")}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="baseUrl">{t("apps.form.baseUrl")}</Label>
              <Input
                id="baseUrl"
                value={baseUrl}
                onChange={(e) => setBaseUrl(e.target.value)}
                placeholder={t("apps.form.baseUrlPlaceholder")}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="availableModels">
                {t("apps.form.availableModels")}
              </Label>
              <Textarea
                id="availableModels"
                value={availableModels}
                onChange={(e) => setAvailableModels(e.target.value)}
                placeholder={t("apps.form.availableModelsPlaceholder")}
                rows={3}
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>{t("apps.form.defaultModel")}</Label>
                <Select value={defaultModel} onValueChange={setDefaultModel}>
                  <SelectTrigger className="w-full">
                    <SelectValue
                      placeholder={t("apps.form.defaultModelPlaceholder")}
                    />
                  </SelectTrigger>
                  <SelectContent>
                    {modelOptions.map((model) => (
                      <SelectItem key={model} value={model}>
                        {model}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="rateLimit">
                  {t("apps.form.rateLimitPerMinute")}
                </Label>
                <Input
                  id="rateLimit"
                  type="number"
                  min="0"
                  value={rateLimitPerMinute}
                  onChange={(e) => setRateLimitPerMinute(e.target.value)}
                  placeholder={t("apps.form.rateLimitPlaceholder")}
                />
              </div>
            </div>
          </div>

          {/* Active Status (only for editing) */}
          {isEditing && (
            <div className="flex items-center justify-between border-t pt-4">
              <Label>{t("apps.form.isActive")}</Label>
              <Switch checked={isActive} onCheckedChange={setIsActive} />
            </div>
          )}

          {/* Actions */}
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={isSubmitting}
            >
              {t("common.cancel")}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
              {t("common.save")}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

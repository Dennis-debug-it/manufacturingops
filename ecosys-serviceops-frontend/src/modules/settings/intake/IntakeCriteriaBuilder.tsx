import { Braces, Plus, Trash2 } from 'lucide-react'
import type { IntakeCriteriaGroup, IntakeProtocolSourceType } from './types'
import {
  createEmptyCriterion,
  createEmptyGroup,
  criteriaOperators,
  emailFieldOptions,
  getCriterionPlaceholder,
  getCriterionValueOptions,
  isBooleanField,
  monitoringFieldOptions,
  operatorNeedsValue,
} from './types'

export function IntakeCriteriaBuilder({
  sourceType,
  groups,
  onChange,
  error,
}: {
  sourceType: IntakeProtocolSourceType
  groups: IntakeCriteriaGroup[]
  onChange: (groups: IntakeCriteriaGroup[]) => void
  error?: string
}) {
  const fieldOptions = sourceType === 'Email' ? emailFieldOptions : monitoringFieldOptions

  function updateGroup(groupId: string, updater: (group: IntakeCriteriaGroup) => IntakeCriteriaGroup) {
    onChange(groups.map((group) => (group.id === groupId ? updater(group) : group)))
  }

  function addGroup() {
    const next = createEmptyGroup()
    next.criteria = [createEmptyCriterion(sourceType === 'Email' ? 'Subject' : 'Alert Name')]
    onChange([...groups, { ...next, joiner: 'OR' }])
  }

  function addCriterion(groupId?: string) {
    if (!groupId) {
      const next = createEmptyGroup()
      next.criteria = [createEmptyCriterion(sourceType === 'Email' ? 'Subject' : 'Alert Name')]
      onChange([next])
      return
    }

    updateGroup(groupId, (group) => ({
      ...group,
      criteria: [...group.criteria, createEmptyCriterion(sourceType === 'Email' ? 'Subject' : 'Alert Name')],
    }))
  }

  return (
    <section className="surface-card h-full space-y-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.24em]">Step 2</p>
          <h2 className="mt-2 text-xl font-semibold tracking-tight text-app">Analysis Conditions</h2>
          <p className="mt-2 text-sm leading-6 text-muted">Build nested intake logic with criteria rows, grouped rules, and explicit AND / OR behavior.</p>
        </div>
        <div className="icon-accent rounded-2xl p-3">
          <Braces className="h-5 w-5" />
        </div>
      </div>

      <div className="flex flex-wrap gap-3">
        <button type="button" className="button-secondary" onClick={() => addCriterion(groups[groups.length - 1]?.id)}>
          <Plus className="h-4 w-4" />
          Add Criteria
        </button>
        <button type="button" className="button-secondary" onClick={() => addGroup()}>
          <Plus className="h-4 w-4" />
          Add Group
        </button>
      </div>
      {error ? <p className="text-sm text-rose-300">{error}</p> : null}

      <div className="space-y-4">
        {groups.map((group, groupIndex) => (
          <div key={group.id} className="rounded-[28px] border border-app bg-white/5 p-4">
            {groupIndex > 0 ? (
              <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-sky-400/30 bg-sky-500/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-sky-100">
                {group.joiner}
                <span className="text-app">Group {groupIndex + 1}</span>
              </div>
            ) : null}

            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="text-sm font-semibold text-app">Group {groupIndex + 1}</p>
                <p className="mt-1 text-sm text-muted">Match all or any conditions within this group.</p>
              </div>
              <div className="flex items-center gap-3">
                {groupIndex > 0 ? (
                  <select value={group.joiner} onChange={(event) => updateGroup(group.id, (current) => ({ ...current, joiner: event.target.value as 'AND' | 'OR' }))} className="field-input w-[110px]">
                    {['AND', 'OR'].map((option) => (
                      <option key={option} value={option}>{option}</option>
                    ))}
                  </select>
                ) : null}
                <select value={group.logic} onChange={(event) => updateGroup(group.id, (current) => ({ ...current, logic: event.target.value as 'AND' | 'OR' }))} className="field-input w-[110px]">
                  {['AND', 'OR'].map((option) => (
                    <option key={option} value={option}>{option}</option>
                  ))}
                </select>
                {groups.length > 1 ? (
                  <button type="button" className="icon-button" onClick={() => onChange(groups.filter((item) => item.id !== group.id))} aria-label="Remove criteria group">
                    <Trash2 className="h-4 w-4" />
                  </button>
                ) : null}
              </div>
            </div>

            <div className="mt-4 space-y-3">
              {group.criteria.map((criterion, criterionIndex) => (
                <div key={criterion.id} className="grid gap-3 rounded-[24px] border border-app/80 bg-white/5 p-4 xl:grid-cols-[1.08fr_0.92fr_1fr_auto]">
                  <div className="xl:col-span-4">
                    <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Criterion {criterionIndex + 1}</p>
                  </div>
                  <div>
                    <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Field</p>
                    <select
                      value={criterion.field}
                      onChange={(event) =>
                        updateGroup(group.id, (current) => ({
                          ...current,
                          criteria: current.criteria.map((item) =>
                            item.id === criterion.id
                              ? {
                                  ...item,
                                  field: event.target.value,
                                  operator: isBooleanField(event.target.value) ? 'Equals' : item.operator,
                                  value: '',
                                  booleanValue: isBooleanField(event.target.value) ? item.booleanValue : false,
                                }
                              : item,
                          ),
                        }))
                      }
                      className="field-input mt-2"
                    >
                      {fieldOptions.map((option) => (
                        <option key={option} value={option}>{option}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Operator</p>
                    <select
                      value={criterion.operator}
                      onChange={(event) =>
                        updateGroup(group.id, (current) => ({
                          ...current,
                          criteria: current.criteria.map((item) =>
                            item.id === criterion.id ? { ...item, operator: event.target.value as typeof criterion.operator } : item,
                          ),
                        }))
                      }
                      className="field-input mt-2"
                    >
                      {criteriaOperators.map((option) => (
                        <option key={option} value={option}>{option}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Value</p>
                    {isBooleanField(criterion.field) ? (
                      <label className="panel-subtle mt-2 flex items-center justify-between rounded-2xl px-4 py-3">
                        <span className="text-sm text-app">Has attachment</span>
                        <input
                          type="checkbox"
                          checked={criterion.booleanValue}
                          onChange={(event) =>
                            updateGroup(group.id, (current) => ({
                              ...current,
                              criteria: current.criteria.map((item) => (item.id === criterion.id ? { ...item, booleanValue: event.target.checked } : item)),
                            }))
                          }
                        />
                      </label>
                    ) : getCriterionValueOptions(criterion.field) ? (
                      <select
                        value={criterion.value}
                        onChange={(event) =>
                          updateGroup(group.id, (current) => ({
                            ...current,
                            criteria: current.criteria.map((item) => (item.id === criterion.id ? { ...item, value: event.target.value } : item)),
                          }))
                        }
                        className="field-input mt-2"
                      >
                        <option value="">Select value</option>
                        {getCriterionValueOptions(criterion.field)?.map((option) => (
                          <option key={option} value={option}>{option}</option>
                        ))}
                      </select>
                    ) : operatorNeedsValue(criterion.operator) ? (
                      <input
                        value={criterion.value}
                        onChange={(event) =>
                          updateGroup(group.id, (current) => ({
                            ...current,
                            criteria: current.criteria.map((item) => (item.id === criterion.id ? { ...item, value: event.target.value } : item)),
                          }))
                        }
                        className="field-input mt-2"
                        placeholder={getCriterionPlaceholder(criterion.field)}
                      />
                    ) : (
                      <div className="panel-subtle mt-2 rounded-2xl px-4 py-3 text-sm text-muted">No value required for this operator.</div>
                    )}
                  </div>
                  <div className="flex items-end justify-end">
                    <button
                      type="button"
                      className="icon-button"
                      onClick={() =>
                        updateGroup(group.id, (current) => ({
                          ...current,
                          criteria: current.criteria.length > 1 ? current.criteria.filter((item) => item.id !== criterion.id) : current.criteria,
                        }))
                      }
                      disabled={group.criteria.length === 1}
                      aria-label={`Remove criterion ${criterionIndex + 1}`}
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </div>
              ))}
            </div>

            <div className="mt-4 flex justify-end">
              <button type="button" className="button-secondary" onClick={() => addCriterion(group.id)}>
                <Plus className="h-4 w-4" />
                Add Criteria
              </button>
            </div>
          </div>
        ))}
      </div>
    </section>
  )
}

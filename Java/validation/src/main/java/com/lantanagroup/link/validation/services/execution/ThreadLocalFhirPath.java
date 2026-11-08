package com.lantanagroup.link.validation.services.execution;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.fhirpath.IFhirPath;
import ca.uhn.fhir.fhirpath.IFhirPathEvaluationContext;
import org.hl7.fhir.instance.model.api.IBase;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;

/**
 * An {@link IFhirPath} that confines HAPI's FHIRPath engine to the calling thread.
 *
 * <p>HAPI's {@code IFhirPath} wraps a single {@code FHIRPathEngine} whose instance state — current
 * location, log buffer, terminology options, profile utilities — is mutable, and HAPI publishes no
 * thread-safety guarantee for it. That was harmless while rubric checks ran one at a time; now that
 * they fan out across a pool, several threads would evaluate against the same engine at once. This
 * decorator hands each thread its own engine, so every injection site stays unchanged and no caller
 * can accidentally share one.
 *
 * <p>Engines are cached per thread rather than built per call because construction is not free: the
 * engine indexes every {@code StructureDefinition} in the context to build its type map. The parsed
 * profiles themselves live in the {@link FhirContext}'s validation support and are shared, so the
 * per-thread cost is that index, not the profiles. One engine is built eagerly at construction so the
 * profile load stays a startup cost rather than landing on the first request.
 *
 * <p>Engines live as long as the threads that created them, which for a pooled or container thread
 * means the lifetime of the process. Memory is therefore bounded by thread count, not by request
 * volume.
 *
 * <p>Two caveats follow from thread confinement:
 * <ul>
 *   <li>{@link #setEvaluationContext(IFhirPathEvaluationContext)} is for wiring time, before any
 *       evaluation happens. The context is remembered and applied to every engine created from then
 *       on, including the calling thread's, but engines already created on <em>other</em> threads keep
 *       the previous one. Do not call it once checks are running.</li>
 *   <li>An {@link IParsedExpression} returned by {@link #parse(String)} comes from one thread's engine
 *       and must not be shared across threads; parse it on the thread that will evaluate it.</li>
 * </ul>
 */
public class ThreadLocalFhirPath implements IFhirPath {

    // Bounds each thread's parsed-expression cache. A rubric has about a hundred expressions; the bound only
    // matters if many different rubrics are evaluated, and then the least recently used are parsed again.
    static final int PARSED_EXPRESSIONS_PER_THREAD = 2_000;

    private final ThreadLocal<IFhirPath> delegate;
    private final ThreadLocal<Map<String, IParsedExpression>> parsedExpressions = ThreadLocal.withInitial(() ->
            new LinkedHashMap<>(256, 0.75f, true) {
                @Override
                protected boolean removeEldestEntry(Map.Entry<String, IParsedExpression> eldest) {
                    return size() > PARSED_EXPRESSIONS_PER_THREAD;
                }
            });
    private volatile IFhirPathEvaluationContext evaluationContext;

    public ThreadLocalFhirPath(FhirContext fhirContext) {
        this.delegate = ThreadLocal.withInitial(() -> {
            IFhirPath created = fhirContext.newFhirPath();
            IFhirPathEvaluationContext context = this.evaluationContext;
            if (context != null) {
                created.setEvaluationContext(context);
            }
            return created;
        });
        // Force the context's validation support to load its profiles now, matching the startup cost
        // of the eagerly-constructed engine this replaced.
        delegate.get();
    }

    /** How many expressions the calling thread has parsed and kept. Package-private for tests. */
    int parsedExpressionCount() {
        return parsedExpressions.get().size();
    }

    /** The calling thread's engine. Package-private for tests. */
    IFhirPath current() {
        return delegate.get();
    }

    @Override
    public <T extends IBase> List<T> evaluate(IBase input, String path, Class<T> returnType) {
        IParsedExpression parsed = parsedOnThisThread(path);
        return parsed != null
                ? current().evaluate(input, parsed, returnType)
                : current().evaluate(input, path, returnType);
    }

    @Override
    public <T extends IBase> List<T> evaluate(IBase input, IParsedExpression path, Class<T> returnType) {
        return current().evaluate(input, path, returnType);
    }

    @Override
    public <T extends IBase> Optional<T> evaluateFirst(IBase input, String path, Class<T> returnType) {
        IParsedExpression parsed = parsedOnThisThread(path);
        return parsed != null
                ? current().evaluateFirst(input, parsed, returnType)
                : current().evaluateFirst(input, path, returnType);
    }

    /**
     * The expression as parsed by this thread's engine, parsing it the first time this thread sees it. A rubric
     * check evaluates the same expression against every resource in a bundle, and parsing it each time was a large
     * share of the check's cost. Returns null when the expression doesn't parse, so the caller evaluates the text
     * and the engine reports the error exactly as before.
     */
    private IParsedExpression parsedOnThisThread(String path) {
        Map<String, IParsedExpression> cache = parsedExpressions.get();
        IParsedExpression expression = cache.get(path);
        if (expression == null && !cache.containsKey(path)) {
            try {
                expression = current().parse(path);
            } catch (Exception e) {
                expression = null;
            }
            cache.put(path, expression);
        }
        return expression;
    }

    @Override
    public <T extends IBase> Optional<T> evaluateFirst(IBase input, IParsedExpression path, Class<T> returnType) {
        return current().evaluateFirst(input, path, returnType);
    }

    @Override
    public IParsedExpression parse(String expression) throws Exception {
        return current().parse(expression);
    }

    @Override
    public void setEvaluationContext(IFhirPathEvaluationContext evaluationContext) {
        this.evaluationContext = evaluationContext;
        current().setEvaluationContext(evaluationContext);
    }
}

package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.shared.exceptions.ValidationException;

import java.util.ArrayList;
import java.util.List;

public abstract class Validator<T> {
    public void validate(T model) {
        List<String> errors = new ArrayList<>();
        if (!isValid(model, errors)) {
            throw new ValidationException(errors);
        }
    }

    public boolean isValid(T model, List<String> errors) {
        doValidate(model, errors);
        return errors.isEmpty();
    }

    protected abstract void doValidate(T model, List<String> errors);
}

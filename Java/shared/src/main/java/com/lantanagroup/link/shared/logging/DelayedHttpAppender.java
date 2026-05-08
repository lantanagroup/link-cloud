package com.lantanagroup.link.shared.logging;

import ch.qos.logback.classic.spi.ILoggingEvent;
import com.github.loki4j.logback.Loki4jAppender;
import org.springframework.boot.context.event.ApplicationReadyEvent;
import org.springframework.context.ApplicationListener;
import org.springframework.stereotype.Component;

import java.util.concurrent.ConcurrentLinkedQueue;

@Component
public class DelayedHttpAppender extends Loki4jAppender implements ApplicationListener<ApplicationReadyEvent> {

  private static boolean applicationReady = false;
  private final ConcurrentLinkedQueue<ILoggingEvent> eventQueue = new ConcurrentLinkedQueue<>();

  @Override
  protected void append (ILoggingEvent eventObject) {
    if(applicationReady) {
      processQueue();
      super.append(eventObject);
    } else {
      eventQueue.add(eventObject);
    }
  }

  @Override
  public void onApplicationEvent (ApplicationReadyEvent event) {
    applicationReady = true;
    processQueue();
  }

  private void processQueue() {
    while (!eventQueue.isEmpty()) {
      super.append(eventQueue.poll());
    }
  }
}

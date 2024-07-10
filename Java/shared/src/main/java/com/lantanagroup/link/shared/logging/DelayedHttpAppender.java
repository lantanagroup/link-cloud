package com.lantanagroup.link.shared.logging;

import ch.qos.logback.classic.spi.ILoggingEvent;
import com.github.loki4j.logback.Loki4jAppender;
import org.springframework.boot.context.event.ApplicationReadyEvent;
import org.springframework.context.ApplicationListener;
import org.springframework.stereotype.Component;

import java.util.concurrent.ConcurrentLinkedQueue;

/*
  * This class is a custom logback appender that extends Loki4jAppender. It is used to delay the logging of messages until the application is ready.
  * This is useful when the application is logging messages before the Loki4jAppender is ready to accept them.
  * The class uses a ConcurrentLinkedQueue to store messages until the application is ready.
  * When the application is ready, the messages are processed and logged.
  * The class also implements ApplicationListener to listen for the ApplicationReadyEvent.
  * When the application is ready, the applicationReady flag is set to true and the processQueue method is called to log the messages in the queue.
  * The append method is overridden to add messages to the queue if the application is not ready.
  * If the application is ready, the processQueue method is called to log the messages in the queue.
  * If the application is ready, the super.append method is called to log the message.
  * The onApplicationEvent method is overridden to set the applicationReady flag to true when the application is ready.
  * The loki appender is added to the logback configuration file to log messages to Loki.
  * The loki url is already properly set from the app config by the time the application is ready. There is no attempt to send messages to Loki using the default url set in application.yml, before the application is ready and the url is properly set.
 */
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

from tkinter import *
from tkinter import ttk

# RangeSlider is a widget that features a two-headed range slider, useful for any situation that
# requires a user to mark 'in' and 'out' points.
# A demo (executed when running this script as main) is provided at the end of this file.

# RangeSlider builds upon MenxLi's 'tkSliderWidget' at https://github.com/MenxLi/tkSliderWidget


class RangeSlider(Frame):
    """RangeSlider presents a double-headed slider to the user.

    """
    LINE_COLOUR = "#476b6b"
    LINE_WIDTH = 3
    HEAD_COLOUR_INNER = "#5c8a8a"
    HEAD_COLOUR_OUTER = "#c2d6d6"
    HEAD_RADIUS = 10
    HEAD_RADIUS_INNER = 5
    HEAD_LINE_WIDTH = 2

    def __init__(self, master, value_min=0, value_max=1, width=400, height=40,
                 value_display=lambda v: f"{v:0.2f}", inverse_display=lambda s: float(s)):
        Frame.__init__(self, master, height=height, width=width)
        self.master = master
        self.user_moved_sliders_since_last_check = False

        self.__value_min = value_min
        self.__value_max = value_max
        self.__width = width
        self.__height = height
        self.__value_display = value_display
        self.__inverse_display = inverse_display

        # It is often necessary to translate the 'position', the x/y co-ordinates on the screen,
        # to and from the 'value', which the sliders are intended to represent.
        # The following functions are the only ones that handle such translations.
        self.__pos_to_value = None
        self.__value_to_pos = None

        self.__value_in = value_min
        self.__value_out = value_max

        self.__slider_x_start = RangeSlider.HEAD_RADIUS
        self.__slider_x_end = self.__width - RangeSlider.HEAD_RADIUS
        self.__slider_y = self.__height * 1 / 2
        self.__bar_offset = (-self.HEAD_RADIUS, -self.HEAD_RADIUS, self.HEAD_RADIUS, self.HEAD_RADIUS)
        self.__selected_head = None  # Bar selected for movement

        # Master canvas element and bindings to left mouse click and clicked move
        self.__canvas = Canvas(self, height=self.__height, width=self.__width)
        self.__canvas.grid(row=0)
        self.__canvas.bind("<Motion>", self.__onclick)
        self.__canvas.bind("<B1-Motion>", self.__clicked_move)

        # Entries for showing user selected values, or allowing user to specify their own
        self.__entry_in_var = StringVar()
        self.__entry_in = ttk.Entry(self, width=len(value_display(value_min)), textvariable=self.__entry_in_var)
        self.__entry_in.grid(row=1, sticky=W)
        self.__entry_out_var = StringVar()
        self.__entry_out = ttk.Entry(self, width=len(value_display(value_max)), textvariable=self.__entry_out_var)
        self.__entry_out.grid(row=1, sticky=E)

        # Slider bar and heads
        self.__canvas.create_line((self.__slider_x_start, self.__slider_y, self.__slider_x_end, self.__slider_y),
                                  fill=RangeSlider.LINE_COLOUR, width=RangeSlider.LINE_WIDTH)
        self.__head_in = self.__add_head(value_min)
        self.__head_out = self.__add_head(value_max)

        # Reset in and out sliders and labels
        self.change_min_max(value_min, value_max, force=True)
        self.change_display(value_display, inverse_display)

    def change_min_max(self, value_min, value_max, reset=True, force=False):
        """Update the minimum and maximum 'values', and adjust the slider heads if/as necessary.

        If the given min and max are unchanged, this function will do nothing unless the optional flag 'force' is True.
        When true (default), the optional flag 'reset' will reset in and out heads to min/max.
        Otherwise, they will be kept at their current value (not position) if possible.
        """
        if self.__value_min != value_min or self.__value_max != value_max or force:
            self.__value_min = value_min
            self.__value_max = value_max

            # Update pos-value conversion functions
            def pos_to_value(p):
                return value_min + (value_max - value_min) * (p - self.__slider_x_start) \
                       / (self.__slider_x_end - self.__slider_x_start)
            self.__pos_to_value = pos_to_value

            def value_to_pos(v):
                return self.__slider_x_start + (self.__slider_x_end - self.__slider_x_start) * (v - value_min) \
                       / (value_max - value_min)
            self.__value_to_pos = value_to_pos

            # Reset the sliders
            if reset:
                self.__value_in = value_min
                self.__value_out = value_max
            else:
                self.__value_in = min(max(self.__value_in, value_min), value_max)
                self.__value_out = max(min(self.__value_out, value_max), value_min)

            self.__move_head(self.__head_in, self.__slider_x_start)
            self.__move_head(self.__head_out, self.__slider_x_end)

            self.user_moved_sliders_since_last_check = False
            self.__update_entry_bindings()

    def change_display(self, value_display, inverse_display=None):
        """Update the function that returns the display text for a given 'value'.

        The single argument should be a function which accepts a single value
        and returns a string corresponding to the desired text.

        Example: """

        self.__value_display = value_display
        self.__inverse_display = inverse_display

        if inverse_display:
            if inverse_display(value_display(self.__value_min)) != self.__value_min or \
                    inverse_display(value_display(self.__value_max)) != self.__value_max:
                self.__inverse_display = None

        label_in_text = self.__value_display(self.__value_in)
        self.__entry_in_var.set(label_in_text)
        self.__entry_in['width'] = max(self.__entry_in['width'], len(label_in_text))

        label_out_text = self.__value_display(self.__value_out)
        self.__entry_out_var.set(label_out_text)
        self.__entry_out['width'] = max(self.__entry_out['width'], len(label_out_text))

        self.__update_entry_bindings()

    @staticmethod
    def timestamp_display_builder(maximum_time_in_seconds=None):
        """A common-use-case utility function for passing to change_display to display timestamps.

        Returns a valid display function that will convert values in seconds to appropriate timestamps
        with relevant formatting and zero-padding for an optionally given maximum time.

        Example: my_range_slider.change_display(*RangeSlider.timestamp_display(2000))
        will generate labels of the form "##:##"
        """
        if not maximum_time_in_seconds or maximum_time_in_seconds > 3599:
            # Include space for 'hours'
            def timestamp_format(h, m, s):
                return f"{h}:{m:02}:{s:02}"
        else:
            def timestamp_format(h, m, s):
                return f"{h * 60 + m:02}:{s:02}"

        def f(total_seconds):
            hours, remaining_seconds = divmod(int(total_seconds), 3600)
            minutes, seconds = divmod(remaining_seconds, 60)
            return timestamp_format(hours, minutes, seconds)

        def inverse(timestamp):
            parts = timestamp.split(":")
            if len(parts) == 3:
                # Hours
                seconds = int(parts[0]) * 3600
            else:
                seconds = 0

            # Minutes and seconds
            seconds += int(parts[-2]) * 60 + int(parts[-1])
            return seconds

        return f, inverse

    def get_in_and_out(self) -> tuple:
        """Obtain the values of the 'in' and 'out' marks.

        Returns (in, out) as a tuple."""
        return self.__value_in, self.__value_out

    def __set_in_and_out(self, value_in, value_out) -> None:
        self.__value_in = value_in
        self.__value_out = value_out

    def have_sliders_moved(self) -> bool:
        """Whether the user has moved the sliders via slider or entry since the last time this function was called.
        """
        flag = self.user_moved_sliders_since_last_check
        self.user_moved_sliders_since_last_check = False
        return flag

    def __check_mouse_collision(self, x, y):
        """Check whether the mouse is clicked on either or both bar heads.

        Returns either one of the heads (self.__head_in or self.__head_out), True (both), or None.
        """

        def is_click_on_bbox(bbox, _x, _y):
            return bbox[0] < _x < bbox[2] and bbox[1] < _y < bbox[3]

        in_bbox = self.__canvas.bbox(self.__head_in[0])
        self.__selected_head = self.__head_in if is_click_on_bbox(in_bbox, x, y) else None

        out_bbox = self.__canvas.bbox(self.__head_out[0])
        if is_click_on_bbox(out_bbox, x, y):
            # If both could have been selected (close enough to overlap), return True
            self.__selected_head = True if self.__selected_head else self.__head_out

        return self.__selected_head

    def __onclick(self, event):
        """Handle behaviour when the left mouse button is clicked.
        """
        self.__selected_head = self.__check_mouse_collision(event.x, event.y)
        cursor = ("hand2" if self.__selected_head else "")
        self.__canvas.config(cursor=cursor)

    def __move_head(self, head: tuple, x):
        """Move the head element to the given x position.
        """
        r = RangeSlider.HEAD_RADIUS
        self.__canvas.coords(head[0], (x - r, self.__slider_y - r, x + r, self.__slider_y + r))
        r = RangeSlider.HEAD_RADIUS_INNER
        self.__canvas.coords(head[1], (x - r, self.__slider_y - r, x + r, self.__slider_y + r))

    def __clicked_move(self, event):
        """Handle movement of slider heads when the mouse is held with a head selected and moved.
        """
        if self.__selected_head:
            centre_x = min(self.__slider_x_end, max(self.__slider_x_start, event.x))
            if self.__selected_head is self.__head_in:
                centre_x = min(self.__value_to_pos(self.__value_out), centre_x)
                bar_value = self.__value_in = self.__pos_to_value(centre_x)
                self.__entry_in_var.set(self.__value_display(bar_value))
            elif self.__selected_head is self.__head_out:
                centre_x = max(self.__value_to_pos(self.__value_in), centre_x)
                bar_value = self.__value_out = self.__pos_to_value(centre_x)
                self.__entry_out_var.set(self.__value_display(bar_value))
            else:
                pos_out = self.__value_to_pos(self.__value_out)
                if centre_x > pos_out:
                    # Select the 'out' bar only when we're clearly pulling it right
                    self.__selected_head = self.__head_out
                else:
                    self.__selected_head = self.__head_in

            self.__move_head(self.__selected_head, centre_x)
            self.user_moved_sliders_since_last_check = True

    def __add_head(self, value) -> tuple:
        """Create a 'head' of two circles at the given 'value'. Returns the IDs of both sub-elements in a tuple.
        """
        if self.__value_to_pos:
            centre_x = self.__value_to_pos(value)
        else:
            centre_x = self.__slider_x_end if value else self.__slider_x_start
        centre_y = self.__slider_y

        r = RangeSlider.HEAD_RADIUS
        outer = self.__canvas.create_oval(centre_x - r, centre_y - r,
                                          centre_x + r, centre_y + r,
                                          fill=RangeSlider.HEAD_COLOUR_OUTER,
                                          width=RangeSlider.HEAD_LINE_WIDTH, outline="", )

        r = RangeSlider.HEAD_RADIUS_INNER
        inner = self.__canvas.create_oval(centre_x - r, centre_y - r,
                                          centre_x + r, centre_y + r,
                                          fill=RangeSlider.HEAD_COLOUR_INNER,
                                          width=RangeSlider.HEAD_LINE_WIDTH, outline="", )

        return outer, inner

    def __update_entry_bindings(self):
        """Update the Entry bindings with functions that allow the user the user to move the heads by entering values.

        Only works if inverse_display is set. Should be called whenever the min/max values change.
        """
        def builder(this_var, this_head, other_var, other_head, parity):
            def f(*args):
                if self.__inverse_display:
                    value_in, value_out = self.get_in_and_out()
                    if parity == 1:
                        this_value, other = value_in, value_out
                    else:
                        this_value, other = value_out, value_in
                    proposed = self.__inverse_display(this_var.get())
                    if proposed != this_value:
                        # Value has changed
                        self.user_moved_sliders_since_last_check = True
                        this_value = min(max(self.__value_min, proposed), self.__value_max)
                        this_var.set(self.__value_display(this_value))

                        if this_value * parity > other * parity:
                            # Suppose user enters value for 'out' less than current 'in'
                            # Most intuitive behaviour would be to set 'in' at 'out'.
                            other = this_value
                            other_var.set(self.__value_display(other))
                            self.__move_head(other_head, self.__value_to_pos(other))
                        self.__move_head(this_head, self.__value_to_pos(this_value))

                        if parity == 1:
                            self.__set_in_and_out(this_value, other)
                        else:
                            self.__set_in_and_out(other, this_value)
            return f

        def do_binding(entry, f):
            entry.unbind('<FocusOut>')
            entry.unbind('<Return>')
            entry.unbind('<Escape>')

            entry.bind('<FocusOut>', f)
            entry.bind('<Return>', f)
            entry.bind('<Escape>', f)

        do_binding(self.__entry_in, builder(
            self.__entry_in_var, self.__head_in,
            self.__entry_out_var, self.__head_out, 1
        ))
        do_binding(self.__entry_out, builder(
            self.__entry_out_var, self.__head_out,
            self.__entry_in_var, self.__head_in, -1
        ))

        state = 'enabled' if self.__inverse_display else 'disabled'
        self.__entry_in['state'] = state
        self.__entry_out['state'] = state

if __name__ == "__main__":
    # Short demo with two sliders - one with numbers 0.0-1.0, the other with timestamps 0:00 - 51:05
    DEMO_MAXIMUM_TIME_IN_SECONDS = 3065  # 51:05

    root = Tk()
    slider = RangeSlider(root)
    slider.grid(row=0)

    timestamp_slider = RangeSlider(root, value_min=0, value_max=DEMO_MAXIMUM_TIME_IN_SECONDS)
    timestamp_slider.grid(row=1)
    timestamp_slider.change_display(*RangeSlider.timestamp_display_builder(DEMO_MAXIMUM_TIME_IN_SECONDS))

    # Bind right-clicking on the window to return the values of 'in' and 'out'.
    # These are the primary outputs of this widget and what you would use in your code.
    # Note that the second widget returns the values in seconds because of the specific setup and not through necessity.

    def log_values(*args):
        print(f"Top slider values: {slider.get_in_and_out()}")
        print(f"Bottom slider values: {timestamp_slider.get_in_and_out()}")
    root.bind('<Button-3>', log_values)

    root.mainloop()
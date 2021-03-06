require File.dirname(__FILE__) + '/../../spec_helper'

describe :enum_next, :shared => true do

  before(:each) do
    @enum = enumerator_class.new(1, :upto, 3)
  end  

  it "returns the next element of the enumeration" do
    @enum.next.should == 1
    @enum.next.should == 2
    @enum.next.should == 3
  end

  it "raises a StopIteration exception at the end of the stream" do
    3.times { @enum.next }
    lambda { @enum.next }.should raise_error(StopIteration)
  end

  it "rewinds the enumerator after raising StopIteration" do
    3.times { @enum.next }
    lambda { @enum.next }.should raise_error(StopIteration)
    @enum.next.should == 1
  end
end
